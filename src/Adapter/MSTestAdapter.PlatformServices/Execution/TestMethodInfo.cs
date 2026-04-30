// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestMethod Info object.
/// </summary>
#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
internal partial class TestMethodInfo : ITestMethod
{
    /// <summary>
    /// Specifies the timeout when it is not set in a test case.
    /// </summary>
    public const int TimeoutWhenNotSet = 0;

    private object? _classInstance;
    private bool _isTestContextSet;
    private bool _isTestCleanupInvoked;

    private ExecutionContext? _executionContext;

#if NETFRAMEWORK
    private object? _hostContext;
#endif

    internal TestMethodInfo(MethodInfo testMethod, TestClassInfo parent)
    {
        DebugEx.Assert(testMethod != null, "TestMethod should not be null");
        DebugEx.Assert(parent != null, "Parent should not be null");

        MethodInfo = testMethod;
        Parent = parent;
        RetryAttribute = GetRetryAttribute();
        TimeoutInfo = GetTestTimeout();
        Executor = GetTestMethodAttribute();
    }

    internal TimeoutInfo TimeoutInfo { get; /*For testing only*/set; }

    internal TestMethodAttribute Executor { get; /*For testing only*/set; }

    internal ITestContext TestContext
    {
        get => field ?? (ITestContext?)TestTools.UnitTesting.TestContext.Current ?? throw ApplicationStateGuard.Unreachable();
        set;
    }

    /// <summary>
    /// Gets a value indicating whether timeout is set.
    /// </summary>
    public bool IsTimeoutSet => TimeoutInfo.Timeout != TimeoutWhenNotSet;

    /// <summary>
    /// Gets the parameter types of the test method.
    /// </summary>
    public ParameterInfo[] ParameterTypes => MethodInfo.GetParameters();

    /// <summary>
    /// Gets the return type of the test method.
    /// </summary>
    public Type ReturnType => MethodInfo.ReturnType;

    /// <summary>
    /// Gets the name of the class declaring the test method.
    /// </summary>
    public string TestClassName => Parent.ClassType.FullName!;

    /// <summary>
    /// Gets the name of the test method.
    /// </summary>
    public string TestMethodName => MethodInfo.Name;

    /// <summary>
    /// Gets the methodInfo for test method.
    /// </summary>
    public MethodInfo MethodInfo { get; }

    /// <summary>
    /// Gets the arguments with which test method is invoked.
    /// </summary>
    public object?[]? Arguments { get; private set; }

    /// <summary>
    /// Gets the parent class Info object.
    /// </summary>
    internal TestClassInfo Parent { get; }

    internal RetryBaseAttribute? RetryAttribute { get; }

    /// <summary>
    /// Gets all attributes of the test method.
    /// </summary>
    /// <returns>An array of the attributes.</returns>
    public Attribute[]? GetAllAttributes()
        => [.. ReflectHelper.Instance.GetAttributes<Attribute>(MethodInfo)];

    /// <summary>
    /// Gets all attributes of the test method.
    /// </summary>
    /// <typeparam name="TAttributeType">The type of the attributes.</typeparam>
    /// <returns>An array of the attributes.</returns>
    public TAttributeType[] GetAttributes<TAttributeType>()
        where TAttributeType : Attribute
        => [.. ReflectHelper.Instance.GetAttributes<TAttributeType>(MethodInfo)];
}
