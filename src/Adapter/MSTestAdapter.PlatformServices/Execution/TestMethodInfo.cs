// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
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

    /// <summary>
    /// Execute test method. Capture failures, handle async and return result.
    /// </summary>
    /// <param name="arguments">
    ///  Arguments to pass to test method. (E.g. For data driven).
    /// </param>
    /// <returns>Result of test method invocation.</returns>
    public virtual async Task<TestResult> InvokeAsync(object?[]? arguments)
    {
        Stopwatch watch = new();
        TestResult? result = null;

        // check if arguments are set for data driven tests
        arguments ??= Arguments;

        watch.Start();

        try
        {
            result = IsTimeoutSet
                ? await ExecuteInternalWithTimeoutAsync(arguments).ConfigureAwait(false)
                : await ExecuteInternalAsync(arguments, null).ConfigureAwait(false);
        }
        finally
        {
            // Handle logs & debug traces.
            watch.Stop();

            if (result != null)
            {
                var testContextImpl = TestContext as TestContextImplementation;
                result.LogOutput = testContextImpl?.GetAndClearOutput();
                result.LogError = testContextImpl?.GetAndClearError();
                result.DebugTrace = testContextImpl?.GetAndClearTrace();
                result.TestContextMessages = TestContext?.GetAndClearDiagnosticMessages();
                result.ResultFiles = TestContext?.GetResultFiles();
                result.Duration = watch.Elapsed;
            }

            _executionContext?.Dispose();
            _executionContext = null;
#if NETFRAMEWORK
            _hostContext = null;
#endif
        }

        return result;
    }

    /// <summary>
    /// Provides the Test Method Extension Attribute of the TestClass.
    /// </summary>
    /// <returns>Test Method Attribute.</returns>
    private TestMethodAttribute GetTestMethodAttribute()
    {
        // Get the derived TestMethod attribute from reflection.
        // It should be non-null as it was already validated by IsValidTestMethod.
        TestMethodAttribute testMethodAttribute = ReflectHelper.Instance.GetSingleAttributeOrDefault<TestMethodAttribute>(MethodInfo)!;

        // Get the derived TestMethod attribute from Extended TestClass Attribute
        // If the extended TestClass Attribute doesn't have extended TestMethod attribute then base class returns back the original testMethod Attribute
        return Parent.ClassAttribute.GetTestMethodAttribute(testMethodAttribute) ?? testMethodAttribute;
    }

    /// <summary>
    /// Gets the number of retries this test method should make in case of failure.
    /// </summary>
    /// <returns>
    /// The number of retries, which is always greater than or equal to 1.
    /// If RetryAttribute is not present, returns 1.
    /// </returns>
    private RetryBaseAttribute? GetRetryAttribute()
    {
        IEnumerable<RetryBaseAttribute> attributes = ReflectHelper.Instance.GetAttributes<RetryBaseAttribute>(MethodInfo);
        using IEnumerator<RetryBaseAttribute> enumerator = attributes.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        RetryBaseAttribute attribute = enumerator.Current;

        if (enumerator.MoveNext())
        {
            ThrowMultipleAttributesException(nameof(RetryBaseAttribute));
        }

        return attribute;
    }
}
