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
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol)
    {
        var classSymbol = (INamedTypeSymbol)context.Symbol;

        if (!classSymbol.IsTestClass(testClassAttributeSymbol))
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
                    if (!WouldRunOrBeDiscoveredIfSameVersion(method, kind, attribute, classSymbol, baseType))
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
        "ClassInitializeAttribute" or "ClassCleanupAttribute" => MSTestMemberKind.ClassFixture,

        // AssemblyInitialize/AssemblyCleanup are intentionally excluded: they are discovered per-assembly, not
        // inherited, so a base library's assembly fixture never runs on a derived test class even after recompilation.
        _ => MSTestMemberKind.None,
    };

    private static bool WouldRunOrBeDiscoveredIfSameVersion(IMethodSymbol method, MSTestMemberKind kind, AttributeData attribute, INamedTypeSymbol testClass, INamedTypeSymbol declaringBaseType)
        // A non-public member, or one replaced by an override or a `new` declaration in a more-derived type, is not
        // the member MSTest would run or discover, so recompiling the base cannot change its behavior.
        => method.DeclaredAccessibility == Accessibility.Public
            && !IsHiddenOrOverriddenInDerivedType(method, testClass, declaringBaseType)
            && kind switch
            {
                // Test methods must be public instance methods that return void/Task/ValueTask (parameters are allowed
                // for data-driven tests).
                MSTestMemberKind.TestMethod
                    => !method.IsStatic && ReturnsVoidTaskOrValueTask(method),

                // Instance fixtures must be public, non-static, parameterless, and return void/Task/ValueTask.
                MSTestMemberKind.InstanceFixture
                    => !method.IsStatic && method.Parameters.IsEmpty && ReturnsVoidTaskOrValueTask(method),

                // Class fixtures must be public static methods with a valid parameter shape (optionally a single
                // TestContext), returning void/Task/ValueTask, and only flow to derived classes when
                // InheritanceBehavior.BeforeEachDerivedClass is set (the default None does not, even in the same version).
                MSTestMemberKind.ClassFixture
                    => method.IsStatic
                        && HasClassFixtureParameterShape(method)
                        && ReturnsVoidTaskOrValueTask(method)
                        && HasBeforeEachDerivedClassBehavior(attribute),

                _ => false,
            };

    private static bool ReturnsVoidTaskOrValueTask(IMethodSymbol method)
        => method.ReturnsVoid
            || (method.ReturnType.ContainingNamespace is { } containingNamespace
                && string.Equals(containingNamespace.ToDisplayString(), "System.Threading.Tasks", StringComparison.Ordinal)
                && (string.Equals(method.ReturnType.Name, "Task", StringComparison.Ordinal)
                    || string.Equals(method.ReturnType.Name, "ValueTask", StringComparison.Ordinal)));

    private static bool HasClassFixtureParameterShape(IMethodSymbol method)
        => method.Parameters.IsEmpty
            || (method.Parameters.Length == 1
                && method.Parameters[0].Type.ContainingNamespace is { } containingNamespace
                && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
                && string.Equals(method.Parameters[0].Type.Name, "TestContext", StringComparison.Ordinal));

    private static bool HasBeforeEachDerivedClassBehavior(AttributeData attribute)
        => attribute.ConstructorArguments.Any(argument =>
            argument.Type is INamedTypeSymbol argumentType
            && string.Equals(argumentType.Name, InheritanceBehaviorTypeName, StringComparison.Ordinal)
            && argumentType.ContainingNamespace is { } containingNamespace
            && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
            && argument.Value is int value
            && value == BeforeEachDerivedClass);

    private static bool IsHiddenOrOverriddenInDerivedType(IMethodSymbol baseMethod, INamedTypeSymbol testClass, INamedTypeSymbol declaringBaseType)
    {
        // MSTest suppresses an inherited member whenever a more-derived type declares a method with the same name,
        // whether it overrides the base virtual method or hides it with `new`.
        for (INamedTypeSymbol? type = testClass;
            type is not null && !SymbolEqualityComparer.Default.Equals(type, declaringBaseType);
            type = type.BaseType)
        {
            if (type.GetMembers(baseMethod.Name).Any(member => member is IMethodSymbol))
            {
                return true;
            }
        }

        return false;
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
        ClassFixture,
    }
}
