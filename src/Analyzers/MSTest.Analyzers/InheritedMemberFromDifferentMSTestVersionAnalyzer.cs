// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0082: <inheritdoc cref="Resources.InheritedMemberFromDifferentMSTestVersionTitle"/>.
/// </summary>
/// <remarks>
/// MSTest matches lifecycle and test attributes (<c>[TestInitialize]</c>, <c>[TestMethod]</c>, …) by exact CLR type
/// identity. The framework assembly was renamed from <c>Microsoft.VisualStudio.TestPlatform.TestFramework</c> (v3) to
/// <c>MSTest.TestFramework</c> (v4), so a base class compiled against a different MSTest major carries attributes
/// whose type identity does not match the ones the current adapter looks for. When such a base is inherited by a test
/// class compiled against another version, the inherited fixtures do not run and the inherited test methods are not
/// discovered — with no build error and no discovery error. This analyzer surfaces that silent mismatch.
/// <para>
/// Limitation: class-fixture inheritance (<c>[ClassInitialize]</c>/<c>[ClassCleanup]</c>) is only detected when the
/// legacy attribute constructor resolves. When the base library's framework assembly is entirely absent from the
/// compilation (a real PE reference whose dependency is not provided), Roslyn cannot bind the attribute constructor
/// and exposes no constructor arguments, so <c>InheritanceBehavior</c> cannot be read and the inherited class fixture
/// is conservatively not reported — treating an undecodable argument as <c>BeforeEachDerivedClass</c> would falsely
/// flag the default <c>None</c>. Instance fixtures and test methods, which carry no constructor arguments, are
/// detected regardless of whether the legacy framework is resolvable.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class InheritedMemberFromDifferentMSTestVersionAnalyzer : DiagnosticAnalyzer
{
    private const string MSTestNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";
    private const string TestClassAttributeName = "TestClassAttribute";
    private const string InheritanceBehaviorTypeName = "InheritanceBehavior";
    private const int BeforeEachDerivedClass = 1;

    private static readonly LocalizableResourceString Title = new(nameof(Resources.InheritedMemberFromDifferentMSTestVersionTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.InheritedMemberFromDifferentMSTestVersionMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.InheritedMemberFromDifferentMSTestVersionDescription), Resources.ResourceManager, typeof(Resources));

    /// <inheritdoc cref="Resources.InheritedMemberFromDifferentMSTestVersionTitle" />
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.InheritedMemberFromDifferentMSTestVersionRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol, taskSymbol, valueTaskSymbol, canDiscoverInternals),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals)
    {
        var classSymbol = (INamedTypeSymbol)context.Symbol;

        // A test class the adapter cannot discover never runs, so no inherited member can run for it: abstract and
        // generic types are rejected, and the type must be visible enough to be discovered (public, or internal when
        // the assembly opts into DiscoverInternals). Concrete, accessible, non-generic derived classes are analyzed
        // separately and still get the warning.
        if (!classSymbol.IsTestClass(testClassAttributeSymbol)
            || classSymbol.IsAbstract
            || classSymbol.IsGenericType
            || !IsDiscoverableTestClassVisibility(classSymbol, canDiscoverInternals))
        {
            return;
        }

        // The test project compiles against exactly one MSTest framework assembly. Anchor on the canonical
        // TestClassAttribute found in the applied attribute's base chain rather than the concrete attribute itself,
        // so a custom [TestClass] subclass (a supported extensibility point) resolves to the framework assembly and
        // does not produce a false positive.
        IAssemblySymbol referenceAssembly = GetFrameworkAssembly(classSymbol) ?? testClassAttributeSymbol.ContainingAssembly;

        for (INamedTypeSymbol? baseType = classSymbol.BaseType;
            baseType is not null && baseType.SpecialType != SpecialType.System_Object;
            baseType = baseType.BaseType)
        {
            // Members declared in the same assembly as the test class bind to the current framework, so they can
            // never be the mismatched version. Only inherited members from another assembly can be affected.
            if (SymbolEqualityComparer.Default.Equals(baseType.ContainingAssembly, classSymbol.ContainingAssembly))
            {
                continue;
            }

            foreach (ISymbol member in baseType.GetMembers())
            {
                if (member is not IMethodSymbol method)
                {
                    continue;
                }

                foreach (AttributeData attribute in method.GetAttributes())
                {
                    // Resolve the canonical MSTest attribute in the applied attribute's base chain, so inherited
                    // custom lifecycle/test attribute subclasses compiled against the old framework are matched too.
                    INamedTypeSymbol? canonicalAttribute = GetCanonicalMSTestAttribute(attribute.AttributeClass);
                    if (canonicalAttribute is null)
                    {
                        continue;
                    }

                    MSTestMemberKind kind = GetMemberKind(canonicalAttribute.Name);
                    if (kind == MSTestMemberKind.None)
                    {
                        continue;
                    }

                    IAssemblySymbol? attributeAssembly = canonicalAttribute.ContainingAssembly;
                    if (attributeAssembly is null
                        || string.Equals(attributeAssembly.Name, referenceAssembly.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Only warn about members that would actually run or be discovered if the base were recompiled
                    // against the current version. Otherwise the remediation ("recompile the base") cannot help and
                    // the diagnostic is a false positive.
                    if (!WouldRunOrBeDiscoveredIfSameVersion(method, kind, attribute, classSymbol, baseType, taskSymbol, valueTaskSymbol))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(classSymbol.CreateDiagnostic(
                        Rule,
                        method.Name,
                        baseType.Name,
                        GetDisplayName(attribute.AttributeClass!),
                        attributeAssembly.Name));
                }
            }
        }
    }

    private static bool IsDiscoverableTestClassVisibility(INamedTypeSymbol classSymbol, bool canDiscoverInternals)
    {
        SymbolVisibility resultantVisibility = classSymbol.GetResultantVisibility();
        return canDiscoverInternals
            ? resultantVisibility != SymbolVisibility.Private
            : resultantVisibility == SymbolVisibility.Public;
    }

    private static IAssemblySymbol? GetFrameworkAssembly(INamedTypeSymbol classSymbol)
        => classSymbol.GetAttributes()
            .Select(attribute => GetCanonicalMSTestAttribute(attribute.AttributeClass))
            .OfType<INamedTypeSymbol>()
            .Where(canonicalAttribute => string.Equals(canonicalAttribute.Name, TestClassAttributeName, StringComparison.Ordinal))
            .Select(canonicalAttribute => canonicalAttribute.ContainingAssembly)
            .FirstOrDefault();

    // Walks the applied attribute's base chain and returns the first ancestor that is a well-known MSTest attribute
    // (TestClass or one of the inheritance-sensitive lifecycle/test attributes).
    private static INamedTypeSymbol? GetCanonicalMSTestAttribute(INamedTypeSymbol? attributeClass)
    {
        for (INamedTypeSymbol? type = attributeClass; type is not null; type = type.BaseType)
        {
            if (type.ContainingNamespace is { } containingNamespace
                && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
                && IsCanonicalAttributeName(type.Name))
            {
                return type;
            }
        }

        return null;
    }

    private static bool IsCanonicalAttributeName(string name)
        => string.Equals(name, TestClassAttributeName, StringComparison.Ordinal)
        || GetMemberKind(name) != MSTestMemberKind.None;

    private static MSTestMemberKind GetMemberKind(string canonicalAttributeName) => canonicalAttributeName switch
    {
        "TestMethodAttribute" or "DataTestMethodAttribute" => MSTestMemberKind.TestMethod,
        "TestInitializeAttribute" or "TestCleanupAttribute" => MSTestMemberKind.InstanceFixture,
        "ClassInitializeAttribute" => MSTestMemberKind.ClassInitialize,
        "ClassCleanupAttribute" => MSTestMemberKind.ClassCleanup,

        // AssemblyInitialize/AssemblyCleanup are intentionally excluded: they are discovered per-assembly, not
        // inherited, so a base library's assembly fixture never runs on a derived test class even after recompilation.
        _ => MSTestMemberKind.None,
    };

    private static bool WouldRunOrBeDiscoveredIfSameVersion(IMethodSymbol method, MSTestMemberKind kind, AttributeData attribute, INamedTypeSymbol testClass, INamedTypeSymbol declaringBaseType, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        // A non-public member, or one replaced by an override or a `new` declaration in a more-derived type, is not the
        // member MSTest would run or discover, so recompiling the base cannot change its behavior.
        => method.DeclaredAccessibility == Accessibility.Public
            && !IsHiddenOrOverriddenInDerivedType(method, kind, testClass, declaringBaseType, taskSymbol, valueTaskSymbol)
            && kind switch
            {
                // Test methods must be public instance methods that return void/Task/ValueTask (parameters are allowed
                // for data-driven tests). A generic test method is still discovered and constructed from its data as long
                // as every type parameter is inferable from the parameters, matching MSTEST0003.
                MSTestMemberKind.TestMethod
                    => !method.IsStatic
                        && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol)
                        && (!method.IsGenericMethod || AllGenericTypeParametersAreInferable(method)),

                // Instance fixtures must be public, non-static, non-generic, parameterless, and return void/Task/ValueTask.
                MSTestMemberKind.InstanceFixture
                    => !method.IsStatic && !method.IsGenericMethod && method.Parameters.IsEmpty && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol),

                // ClassInitialize must be a public static non-generic method taking exactly one TestContext, returning
                // void/Task/ValueTask, and only flows to derived classes when BeforeEachDerivedClass is set.
                MSTestMemberKind.ClassInitialize
                    => method.IsStatic
                        && !method.IsGenericMethod
                        && HasSingleTestContextParameter(method)
                        && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol)
                        && HasBeforeEachDerivedClassBehavior(attribute),

                // ClassCleanup is like ClassInitialize but its TestContext parameter is optional.
                MSTestMemberKind.ClassCleanup
                    => method.IsStatic
                        && !method.IsGenericMethod
                        && HasOptionalTestContextParameter(method)
                        && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol)
                        && HasBeforeEachDerivedClassBehavior(attribute),

                _ => false,
            };

    // A generic test method is only discoverable when every type parameter can be inferred from the parameter list,
    // mirroring MSTEST0003's rule (e.g. 'T' must appear as 'T', 'T[]', or 'List&lt;T&gt;' in some parameter).
    private static bool AllGenericTypeParametersAreInferable(IMethodSymbol method)
    {
        foreach (ITypeParameterSymbol typeParameter in method.TypeParameters)
        {
            if (!method.Parameters.Any(parameter => IsOrHasTypeParameter(parameter.Type, typeParameter)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOrHasTypeParameter(ITypeSymbol type, ITypeParameterSymbol typeParameter)
    {
        if (SymbolEqualityComparer.Default.Equals(type, typeParameter))
        {
            return true;
        }

        if (type is IArrayTypeSymbol array)
        {
            return IsOrHasTypeParameter(array.ElementType, typeParameter);
        }

        if (type is INamedTypeSymbol namedType)
        {
            foreach (ITypeSymbol typeArgument in namedType.TypeArguments)
            {
                if (IsOrHasTypeParameter(typeArgument, typeParameter))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A fixture/test method must return void (non-async), non-generic Task, or non-generic ValueTask. Comparing against
    // the resolved Task/ValueTask symbols rejects Task&lt;T&gt;/ValueTask&lt;T&gt;, and the async check rejects async void.
    private static bool ReturnsVoidTaskOrValueTask(IMethodSymbol method, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        => method.ReturnsVoid
            ? !method.IsAsync
            : SymbolEqualityComparer.Default.Equals(method.ReturnType, taskSymbol)
                || SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTaskSymbol);

    private static bool HasSingleTestContextParameter(IMethodSymbol method)
        => method.Parameters.Length == 1 && IsTestContext(method.Parameters[0].Type);

    private static bool HasOptionalTestContextParameter(IMethodSymbol method)
        => method.Parameters.IsEmpty || HasSingleTestContextParameter(method);

    private static bool IsTestContext(ITypeSymbol type)
        => type.ContainingNamespace is { } containingNamespace
            && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
            && string.Equals(type.Name, "TestContext", StringComparison.Ordinal);

    // Reads InheritanceBehavior from the applied attribute's constructor arguments. When the legacy framework assembly
    // is entirely absent from the compilation the attribute constructor cannot bind and ConstructorArguments is empty,
    // so this returns false and the inherited class fixture is conservatively not reported. See the type-level remarks.
    private static bool HasBeforeEachDerivedClassBehavior(AttributeData attribute)
        => attribute.ConstructorArguments.Any(argument =>
            argument.Type is INamedTypeSymbol argumentType
            && string.Equals(argumentType.Name, InheritanceBehaviorTypeName, StringComparison.Ordinal)
            && argumentType.ContainingNamespace is { } containingNamespace
            && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
            && argument.Value is int value
            && value == BeforeEachDerivedClass);

    private static bool IsHiddenOrOverriddenInDerivedType(IMethodSymbol baseMethod, MSTestMemberKind kind, INamedTypeSymbol testClass, INamedTypeSymbol declaringBaseType, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
    {
        // Class fixtures are static and accumulated independently by the adapter, so a same-named derived method does
        // not suppress them (and static methods cannot be overridden anyway).
        if (kind is MSTestMemberKind.ClassInitialize or MSTestMemberKind.ClassCleanup)
        {
            return false;
        }

        for (INamedTypeSymbol? type = testClass;
            type is not null && !SymbolEqualityComparer.Default.Equals(type, declaringBaseType);
            type = type.BaseType)
        {
            foreach (ISymbol member in type.GetMembers(baseMethod.Name))
            {
                if (member is not IMethodSymbol derivedMethod)
                {
                    continue;
                }

                // An override always replaces the inherited member.
                if (Overrides(derivedMethod, baseMethod))
                {
                    return true;
                }

                switch (kind)
                {
                    // An inherited instance fixture is only suppressed when the hiding method is itself a valid
                    // instance fixture; a private/static/parameterized same-name method leaves the base one running.
                    case MSTestMemberKind.InstanceFixture
                        when derivedMethod is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }
                            && derivedMethod.Parameters.IsEmpty
                            && ReturnsVoidTaskOrValueTask(derivedMethod, taskSymbol, valueTaskSymbol):
                        return true;

                    // Test-method hiding is signature-based: only a same-signature method shadows the inherited test;
                    // a different-arity, by-ref, or otherwise different-signature overload stays discoverable.
                    case MSTestMemberKind.TestMethod when HaveSameSignature(derivedMethod, baseMethod):
                        return true;
                }
            }
        }

        return false;
    }

    private static bool Overrides(IMethodSymbol candidate, IMethodSymbol baseMethod)
    {
        for (IMethodSymbol? overridden = candidate.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, baseMethod.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HaveSameSignature(IMethodSymbol left, IMethodSymbol right)
    {
        if (left.Arity != right.Arity || left.Parameters.Length != right.Parameters.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Parameters.Length; index++)
        {
            IParameterSymbol leftParameter = left.Parameters[index];
            IParameterSymbol rightParameter = right.Parameters[index];

            // ref/out/in all share the same by-ref CLR signature, so compare by-value versus by-ref rather than the
            // exact RefKind (a derived 'Run(out int)' still hides a base 'Run(ref int)').
            if ((leftParameter.RefKind == RefKind.None) != (rightParameter.RefKind == RefKind.None)
                || !SymbolEqualityComparer.Default.Equals(leftParameter.Type, rightParameter.Type))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetDisplayName(INamedTypeSymbol attributeClass)
    {
        const string attributeSuffix = "Attribute";
        string name = attributeClass.Name;
        return name.EndsWith(attributeSuffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - attributeSuffix.Length)
            : name;
    }

    private enum MSTestMemberKind
    {
        None,
        TestMethod,
        InstanceFixture,
        ClassInitialize,
        ClassCleanup,
    }
}
