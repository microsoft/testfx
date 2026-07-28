// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0037: Use proper 'Assert' methods.
/// </summary>
/// <remarks>
/// The analyzer captures the following cases:
/// <list type="bullet">
/// <item>
/// <description>
/// <code>Assert.[IsTrue|IsFalse](x [==|!=|is|is not] null)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.[IsTrue|IsFalse](x [==|!=] y)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.AreEqual([true|false], x)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.[AreEqual|AreNotEqual](null, x)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myString.[StartsWith|EndsWith|Contains]("..."))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsFalse(myString.[StartsWith|EndsWith|Contains]("..."))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myCollection.Contains(...))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsFalse(myCollection.Contains(...))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.[IsTrue|IsFalse](x [&gt;|&gt;=|&lt;|&lt;=] y)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.AreEqual([0|X], myCollection.[Count|Length])</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.AreEqual([0|X], myEnumerable.Count())</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.AreNotEqual(0, myCollection.[Count|Length])</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.AreNotEqual(0, myEnumerable.Count())</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myCollection.[Count|Length] [&gt;|!=|==] 0)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myEnumerable.Count() [&gt;|!=|==] 0)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myEnumerable.Any())</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsFalse(myEnumerable.Any())</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myEnumerable.Contains(item, comparer))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsFalse(myEnumerable.Contains(item, comparer))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.HasCount(0, myCollection)</code>
/// </description>
/// </item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed partial class UseProperAssertMethodsAnalyzer : DiagnosticAnalyzer
{
    internal const string ProperAssertMethodNameKey = nameof(ProperAssertMethodNameKey);

    /// <summary>
    /// Only the presence of this key in properties bag indicates that a cast is needed.
    /// The value of the key is always null.
    /// </summary>
    internal const string NeedsNullableBooleanCastKey = nameof(NeedsNullableBooleanCastKey);

    /// <summary>
    /// Key in the properties bag that has value one of CodeFixModeSimple, CodeFixModeAddArgument, or CodeFixModeRemoveArgument.
    /// </summary>
    internal const string CodeFixModeKey = nameof(CodeFixModeKey);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Replace the syntax node from the first additional locations with the node from second additional locations.</item>
    /// </list>
    /// <para>Example: For <c>Assert.IsTrue(x == null)</c>, it will become <c>Assert.IsNull(x)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "IsNull".</para>
    /// <para>The first additional location will point to the "x == null" node.</para>
    /// <para>The second additional location will point to the "x" node.</para>
    /// <para>Optionally, more additional locations will also be interpreted as "replace" operations.</para>
    /// </summary>
    internal const string CodeFixModeSimple = nameof(CodeFixModeSimple);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Replace the syntax node from the first additional locations with the node from second additional locations.</item>
    /// <item>Add new argument which is identical to the node from third additional locations.</item>
    /// </list>
    /// <para>Example: For <c>Assert.IsTrue(x == y)</c>, it will become <c>Assert.AreEqual(y, x)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "AreEqual".</para>
    /// <para>The first additional location will point to the "x == y" node.</para>
    /// <para>The second additional location will point to the "y" node.</para>
    /// <para>The third additional location will point to the "x" node.</para>
    /// </summary>
    internal const string CodeFixModeAddArgument = nameof(CodeFixModeAddArgument);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Remove the argument which the first additional location points to.</item>
    /// </list>
    /// <para>Example: For <c>Assert.AreEqual(false, x)</c>, it will become <c>Assert.IsFalse(x)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "IsFalse".</para>
    /// <para>The first additional location will point to the "false" node.</para>
    /// <para>The second additional location will point to the "x" node, in case a cast is needed.</para>
    /// </summary>
    /// <remarks>
    /// If <see cref="NeedsNullableBooleanCastKey"/> is present, then the produced code will be <c>Assert.IsFalse((bool?)x);</c>.
    /// </remarks>
    internal const string CodeFixModeRemoveArgument = nameof(CodeFixModeRemoveArgument);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Remove the argument which the first additional location points to.</item>
    /// <item>Replace the argument which the second additional location points to with the expression pointed to by the third additional location</item>
    /// </list>
    /// <para>Example: For <c>Assert.AreEqual(0, list.Count)</c>, it will become <c>Assert.IsEmpty(list)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "IsEmpty".</para>
    /// <para>The first additional location will point to the "0" node.</para>
    /// <para>The second additional location will point to the "list.Count" node.</para>
    /// <para>The third additional location will point to the "list" node.</para>
    /// </summary>
    internal const string CodeFixModeRemoveArgumentAndReplaceArgument = nameof(CodeFixModeRemoveArgumentAndReplaceArgument);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Remove the argument which the first additional location points to.</item>
    /// <item>Replace the argument which the second additional location points to with the expression pointed to by the third additional location</item>
    /// <item>Add new argument which is identical to the node from fourth additional locations.</item>
    /// </list>
    /// <para>Example: For <c>Assert.AreEqual(1, collection.Count(x => x == 1))</c>, it will become <c>Assert.ContainsSingle(x => x == 1, collection)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "ContainsSingle".</para>
    /// <para>The first additional location will point to the "1" node.</para>
    /// <para>The second additional location will point to the "collection.Count(x => x == 1)" node.</para>
    /// <para>The third additional location will point to the "x => x == 1" node.</para>
    /// <para>The fourth additional location will point to the "collection" node.</para>
    /// </summary>
    internal const string CodeFixModeRemoveArgumentReplaceArgumentAndAddArgument = nameof(CodeFixModeRemoveArgumentReplaceArgumentAndAddArgument);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Replace the syntax node from the first additional locations with three new arguments from the second, third, and fourth additional locations.</item>
    /// </list>
    /// <para>Example: For <c>Assert.IsTrue(collection.Contains(item, comparer))</c>, it will become <c>Assert.Contains(item, collection, comparer)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "Contains".</para>
    /// <para>The first additional location will point to the "collection.Contains(item, comparer)" node.</para>
    /// <para>The second additional location will point to the "item" node.</para>
    /// <para>The third additional location will point to the "collection" node.</para>
    /// <para>The fourth additional location will point to the "comparer" node.</para>
    /// </summary>
    internal const string CodeFixModeAddTwoArguments = nameof(CodeFixModeAddTwoArguments);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseProperAssertMethodsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseProperAssertMethodsMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseProperAssertMethodsRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Info,
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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertTypeSymbol))
            {
                return;
            }

            INamedTypeSymbol objectTypeSymbol = context.Compilation.GetSpecialType(SpecialType.System_Object);
            context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemLinqEnumerable, out INamedTypeSymbol? enumerableTypeSymbol);
            INamedTypeSymbol? iComparableOfTSymbol = context.Compilation.GetTypeByMetadataName("System.IComparable`1");
            context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemCollectionsIDictionary, out INamedTypeSymbol? iDictionaryTypeSymbol);

            context.RegisterOperationAction(context => AnalyzeInvocationOperation(context, assertTypeSymbol, objectTypeSymbol, enumerableTypeSymbol, iComparableOfTSymbol, iDictionaryTypeSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol assertTypeSymbol, INamedTypeSymbol objectTypeSymbol, INamedTypeSymbol? enumerableTypeSymbol, INamedTypeSymbol? iComparableOfTSymbol, INamedTypeSymbol? iDictionaryTypeSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;
        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, assertTypeSymbol))
        {
            return;
        }

        if (!TryGetFirstArgumentValue(operation, out IOperation? firstArgument))
        {
            return;
        }

        switch (targetMethod.Name)
        {
            case "IsTrue":
                AnalyzeIsTrueOrIsFalseInvocation(context, firstArgument, isTrueInvocation: true, objectTypeSymbol, enumerableTypeSymbol, iComparableOfTSymbol, iDictionaryTypeSymbol);
                break;

            case "IsFalse":
                AnalyzeIsTrueOrIsFalseInvocation(context, firstArgument, isTrueInvocation: false, objectTypeSymbol, enumerableTypeSymbol, iComparableOfTSymbol, iDictionaryTypeSymbol);
                break;

            case "AreEqual":
                AnalyzeAreEqualOrAreNotEqualInvocation(context, firstArgument, isAreEqualInvocation: true, assertTypeSymbol, objectTypeSymbol, enumerableTypeSymbol);
                break;

            case "AreNotEqual":
                AnalyzeAreEqualOrAreNotEqualInvocation(context, firstArgument, isAreEqualInvocation: false, assertTypeSymbol, objectTypeSymbol, enumerableTypeSymbol);
                break;

            case "HasCount":
                AnalyzeHasCountInvocation(context, firstArgument);
                break;
        }
    }
}
