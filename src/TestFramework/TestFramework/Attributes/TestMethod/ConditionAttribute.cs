// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Conditionally runs or ignores a test class or test method based on the value of one or more
/// <see langword="static"/> <see cref="bool"/> members (property, field, or parameterless method)
/// referenced by <see cref="Type"/> and member name.
/// </summary>
/// <remarks>
/// <para>
/// When multiple member names are supplied to a single attribute, their values are combined with
/// a logical AND: the attribute's <see cref="IsConditionMet"/> is <see langword="true"/> only if
/// every referenced member evaluates to <see langword="true"/>.
/// </para>
/// <para>
/// Each <see cref="ConditionAttribute"/> instance forms its own <see cref="ConditionBaseAttribute.GroupName"/>,
/// so stacking multiple <see cref="ConditionAttribute"/> declarations on the same target is combined
/// with a logical AND, matching the typical <c>[ConditionalFact]</c> usage pattern in other test frameworks.
/// </para>
/// <para>
/// If the referenced member cannot be found as a <see langword="public"/> <see langword="static"/>
/// <see cref="bool"/> property, field, or parameterless method, or (for methods) requires parameters,
/// evaluating <see cref="IsConditionMet"/> throws an <see cref="InvalidOperationException"/>. This
/// surfaces as a test error rather than a silent skip so typos and refactors don't accidentally
/// disable tests.
/// </para>
/// <para>
/// This attribute isn't inherited. Applying it to a base class will not affect derived classes.
/// </para>
/// <example>
/// <code>
/// [TestMethod]
/// [Condition(typeof(Environment), nameof(Environment.Is64BitProcess))]
/// public void Only_Runs_On_64Bit() { }
///
/// [TestMethod]
/// [Condition(typeof(PlatformDetection),
///     nameof(PlatformDetection.IsNotBrowser),
///     nameof(PlatformDetection.IsThreadingSupported))]
/// public void Requires_Threading_And_Not_Browser() { }
///
/// [TestMethod]
/// [Condition(ConditionMode.Exclude, typeof(PlatformDetection), nameof(PlatformDetection.IsMonoRuntime))]
/// public void Does_Not_Run_On_Mono() { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class ConditionAttribute : ConditionBaseAttribute
{
    private const DynamicallyAccessedMemberTypes RequiredMembers =
        DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.PublicMethods;

    private readonly string[] _conditionMemberNames;
    private string? _groupName;
    private ReadOnlyCollection<string>? _conditionMemberNamesView;
    private Func<bool>[]? _evaluators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionAttribute"/> class with
    /// <see cref="ConditionMode.Include"/> semantics: the test runs only when the referenced
    /// member evaluates to <see langword="true"/>.
    /// </summary>
    /// <param name="conditionType">The type declaring the static member to evaluate.</param>
    /// <param name="conditionMemberName">
    /// The name of the <see langword="public"/> <see langword="static"/> <see cref="bool"/> member
    /// (property, field, or parameterless method) to evaluate.
    /// </param>
    public ConditionAttribute(
        [DynamicallyAccessedMembers(RequiredMembers)] Type conditionType,
        string conditionMemberName)
        : this(ConditionMode.Include, conditionType, conditionMemberName, additionalConditionMemberNames: [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionAttribute"/> class with
    /// <see cref="ConditionMode.Include"/> semantics: the test runs only when every referenced
    /// member evaluates to <see langword="true"/>.
    /// </summary>
    /// <param name="conditionType">The type declaring the static member(s) to evaluate.</param>
    /// <param name="conditionMemberName">
    /// The name of the first <see langword="public"/> <see langword="static"/> <see cref="bool"/>
    /// member (property, field, or parameterless method) to evaluate.
    /// </param>
    /// <param name="additionalConditionMemberNames">
    /// Additional <see langword="public"/> <see langword="static"/> <see cref="bool"/> member
    /// name(s) to evaluate. All referenced members are AND-combined.
    /// </param>
    [CLSCompliant(false)]
    public ConditionAttribute(
        [DynamicallyAccessedMembers(RequiredMembers)] Type conditionType,
        string conditionMemberName,
        params string[] additionalConditionMemberNames)
        : this(ConditionMode.Include, conditionType, conditionMemberName, additionalConditionMemberNames)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">
    /// Whether the test should be included (run when the condition is met) or excluded
    /// (skipped when the condition is met).
    /// </param>
    /// <param name="conditionType">The type declaring the static member to evaluate.</param>
    /// <param name="conditionMemberName">
    /// The name of the <see langword="public"/> <see langword="static"/> <see cref="bool"/> member
    /// (property, field, or parameterless method) to evaluate.
    /// </param>
    public ConditionAttribute(
        ConditionMode mode,
        [DynamicallyAccessedMembers(RequiredMembers)] Type conditionType,
        string conditionMemberName)
        : this(mode, conditionType, conditionMemberName, additionalConditionMemberNames: [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">
    /// Whether the test should be included (run when the condition is met) or excluded
    /// (skipped when the condition is met).
    /// </param>
    /// <param name="conditionType">The type declaring the static member(s) to evaluate.</param>
    /// <param name="conditionMemberName">
    /// The name of the first <see langword="public"/> <see langword="static"/> <see cref="bool"/>
    /// member (property, field, or parameterless method) to evaluate.
    /// </param>
    /// <param name="additionalConditionMemberNames">
    /// Additional <see langword="public"/> <see langword="static"/> <see cref="bool"/> member
    /// name(s) to evaluate. All referenced members are AND-combined.
    /// </param>
    [CLSCompliant(false)]
    public ConditionAttribute(
        ConditionMode mode,
        [DynamicallyAccessedMembers(RequiredMembers)] Type conditionType,
        string conditionMemberName,
        params string[] additionalConditionMemberNames)
        : base(mode)
    {
        ConditionType = conditionType ?? throw new ArgumentNullException(nameof(conditionType));
        if (conditionMemberName is null)
        {
            throw new ArgumentNullException(nameof(conditionMemberName));
        }

        if (StringEx.IsNullOrWhiteSpace(conditionMemberName))
        {
            throw new ArgumentException(
                "Condition member name must not be empty or whitespace.",
                nameof(conditionMemberName));
        }

        if (additionalConditionMemberNames is null || additionalConditionMemberNames.Length == 0)
        {
            _conditionMemberNames = [conditionMemberName];
        }
        else
        {
            _conditionMemberNames = new string[additionalConditionMemberNames.Length + 1];
            _conditionMemberNames[0] = conditionMemberName;
            for (int i = 0; i < additionalConditionMemberNames.Length; i++)
            {
                string name = additionalConditionMemberNames[i];
                if (StringEx.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException(
                        "Condition member names must not be null, empty, or whitespace.",
                        nameof(additionalConditionMemberNames));
                }

                _conditionMemberNames[i + 1] = name;
            }
        }

        IgnoreMessage = mode == ConditionMode.Include
            ? $"Test is only supported when ({FormatMemberList()}) on '{conditionType.FullName ?? conditionType.Name}' is true."
            : $"Test is not supported when ({FormatMemberList()}) on '{conditionType.FullName ?? conditionType.Name}' is true.";
    }

    /// <summary>
    /// Gets the type declaring the <see langword="static"/> member(s) used to evaluate the condition.
    /// </summary>
    [DynamicallyAccessedMembers(RequiredMembers)]
    public Type ConditionType { get; }

    /// <summary>
    /// Gets the name(s) of the <see langword="static"/> <see cref="bool"/> member(s) (property,
    /// field, or parameterless method) on <see cref="ConditionType"/> evaluated for this condition.
    /// Multiple values are combined with a logical AND.
    /// </summary>
    public IReadOnlyList<string> ConditionMemberNames
        => _conditionMemberNamesView ??= new ReadOnlyCollection<string>(_conditionMemberNames);

    /// <inheritdoc />
    /// <remarks>
    /// Each <see cref="ConditionAttribute"/> instance produces a group name derived from
    /// <see cref="ConditionType"/>, <see cref="ConditionMemberNames"/>, and
    /// <see cref="ConditionBaseAttribute.Mode"/>, so stacking multiple <see cref="ConditionAttribute"/>
    /// declarations on the same target combines them with a logical AND -- including pairs with
    /// the same type/members but opposite <see cref="ConditionMode"/> values, which would otherwise
    /// silently cancel each other out.
    /// </remarks>
    public override string GroupName
        => _groupName ??= $"{nameof(ConditionAttribute)}:{ConditionType.FullName ?? ConditionType.Name}:{string.Join("|", _conditionMemberNames)}:{Mode}";

    /// <inheritdoc />
    /// <remarks>
    /// All referenced members are evaluated in order and combined with a logical AND. Throws
    /// <see cref="InvalidOperationException"/> if a member can't be resolved as a
    /// <see langword="public"/> <see langword="static"/> <see cref="bool"/> property, field, or
    /// parameterless method. Resolved members are cached after the first access so subsequent
    /// evaluations don't pay the reflection cost again.
    /// </remarks>
    public override bool IsConditionMet
    {
        get
        {
            Func<bool>[] evaluators = _evaluators ??= BuildEvaluators();
            return evaluators.All(static evaluator => evaluator());
        }
    }

    private Func<bool>[] BuildEvaluators()
    {
        var evaluators = new Func<bool>[_conditionMemberNames.Length];
        for (int i = 0; i < _conditionMemberNames.Length; i++)
        {
            evaluators[i] = BuildEvaluator(_conditionMemberNames[i]);
        }

        return evaluators;
    }

    private Func<bool> BuildEvaluator(string memberName)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;
        string typeName = ConditionType.FullName ?? ConditionType.Name;

        PropertyInfo? property = ConditionType.GetProperty(memberName, Flags);
        if (property is not null)
        {
            return property.PropertyType != typeof(bool)
                || property.GetIndexParameters().Length != 0
                || property.GetGetMethod(nonPublic: true) is null
                ? throw new InvalidOperationException(
                    $"Member '{typeName}.{memberName}' must be a public static bool readable parameterless property to be used with [Condition].")
                : () => (bool)property.GetValue(null)!;
        }

        FieldInfo? field = ConditionType.GetField(memberName, Flags);
        if (field is not null)
        {
            return field.FieldType != typeof(bool)
                ? throw new InvalidOperationException(
                    $"Member '{typeName}.{memberName}' must be a public static bool field to be used with [Condition].")
                : () => (bool)field.GetValue(null)!;
        }

        MethodInfo? method = ConditionType.GetMethod(memberName, Flags, binder: null, types: Type.EmptyTypes, modifiers: null)
            ?? throw new InvalidOperationException(
                $"Could not find a public static bool property, field, or parameterless method named '{memberName}' on type '{typeName}'.");

        return method.ReturnType != typeof(bool)
            ? throw new InvalidOperationException(
                $"Member '{typeName}.{memberName}' must be a public static parameterless bool method to be used with [Condition].")
            : () => (bool)method.Invoke(null, null)!;
    }

    private string FormatMemberList()
        => _conditionMemberNames.Length == 1
            ? _conditionMemberNames[0]
            : string.Join(" AND ", _conditionMemberNames);
}
