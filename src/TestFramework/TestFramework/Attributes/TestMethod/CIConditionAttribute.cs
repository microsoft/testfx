// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to conditionally control whether a test class or a test method will run or be ignored based on whether the test is running in a CI environment.
/// </summary>
/// <remarks>
/// This attribute isn't inherited. Applying it to a base class will not affect derived classes.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CIConditionAttribute : ConditionBaseAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CIConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">Decides whether the test should be included or excluded in CI environments.</param>
    public CIConditionAttribute(ConditionMode mode)
        : base(mode)
        => IgnoreMessage = mode == ConditionMode.Include
            ? "Test is only supported in CI environments"
            : "Test is not supported in CI environments";

    /// <inheritdoc />
    public override bool IsConditionMet => CIEnvironmentDetector.Instance.IsCIEnvironment();

    /// <summary>
    /// Gets the group name for this attribute.
    /// </summary>
    public override string GroupName => nameof(CIConditionAttribute);
}
