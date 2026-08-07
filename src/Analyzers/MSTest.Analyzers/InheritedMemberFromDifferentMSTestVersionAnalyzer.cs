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

        // The test project compiles against exactly one MSTest framework assembly. Anchor on the assembly that
        // provides the [TestClass] attribute actually applied to this (source-defined) test class, because source
        // binding is deterministic even when a differently-versioned framework is also referenced transitively.
        IAssemblySymbol? referenceAssembly = null;
        foreach (AttributeData attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass.Inherits(testClassAttributeSymbol))
            {
                referenceAssembly = attribute.AttributeClass!.ContainingAssembly;
                break;
            }
        }

        referenceAssembly ??= testClassAttributeSymbol.ContainingAssembly;
        if (referenceAssembly is null)
        {
            return;
        }

        // Only base classes can contribute inherited members. Members declared on the test class itself bind to the
        // current framework by construction, so they can never be the mismatched version.
        for (INamedTypeSymbol? baseType = classSymbol.BaseType;
            baseType is not null && baseType.SpecialType != SpecialType.System_Object;
            baseType = baseType.BaseType)
        {
            foreach (ISymbol member in baseType.GetMembers())
            {
                if (member is not IMethodSymbol)
                {
                    continue;
                }

                foreach (AttributeData attribute in member.GetAttributes())
                {
                    if (GetMSTestLifecycleOrTestAttributeName(attribute.AttributeClass) is not { } attributeName)
                    {
                        continue;
                    }

                    IAssemblySymbol? attributeAssembly = attribute.AttributeClass!.ContainingAssembly;
                    if (attributeAssembly is null
                        || string.Equals(attributeAssembly.Name, referenceAssembly.Name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    context.ReportDiagnostic(classSymbol.CreateDiagnostic(
                        Rule,
                        member.Name,
                        baseType.Name,
                        attributeName,
                        attributeAssembly.Name));
                }
            }
        }
    }

    private static string? GetMSTestLifecycleOrTestAttributeName(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass is null
            || attributeClass.ContainingNamespace is not { } containingNamespace
            || !string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal))
        {
            return null;
        }

        // Attributes whose effect is inheritance-sensitive and matched by exact type identity at run time. When the
        // declaring type comes from a foreign framework assembly these are silently ignored on the derived class.
        return attributeClass.Name switch
        {
            "TestMethodAttribute" => "TestMethod",
            "DataTestMethodAttribute" => "DataTestMethod",
            "TestInitializeAttribute" => "TestInitialize",
            "TestCleanupAttribute" => "TestCleanup",
            "ClassInitializeAttribute" => "ClassInitialize",
            "ClassCleanupAttribute" => "ClassCleanup",
            "AssemblyInitializeAttribute" => "AssemblyInitialize",
            "AssemblyCleanupAttribute" => "AssemblyCleanup",
            _ => null,
        };
    }
}
