// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines the TestMethod Info object.
/// </summary>
#pragma warning disable CA1852 // Seal internal types - This class is inherited in tests.
[StackTraceHidden]
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
    /// <remarks>
    /// Lazy-cached: <c>MethodInfo.GetParameters()</c> returns a fresh array copy on every call
    /// (CLR safety guarantee), so caching avoids N redundant copies for data-driven tests with N rows.
    /// This cached array is shared across internal call sites and MUST NOT be mutated. The explicit
    /// <see cref="ITestMethod.ParameterTypes"/> implementation hands external consumers a fresh copy to
    /// preserve the previous "fresh array per call" behavior and protect the cache from mutation.
    /// </remarks>
    public ParameterInfo[] ParameterTypes => field ??= MethodInfo.GetParameters();

    /// <inheritdoc />
    ParameterInfo[] ITestMethod.ParameterTypes => (ParameterInfo[])ParameterTypes.Clone();

    // Immutable parameter metadata (params-parameter index and required-parameter count) is a pure function
    // of the MethodInfo. It is cached at process scope keyed by MethodInfo so that ResolveArguments does not
    // re-run a reflective attribute scan for it. This is shared across TestMethodInfo instances, so even the
    // default execution path — where discovery unfolds data sources and each row constructs a fresh
    // TestMethodInfo — performs the scan only once per test method rather than once per row.
    // The Lazy ensures the scan runs exactly once per method even when method-level parallelization lets
    // several unfolded rows reach the miss path concurrently.
    private static readonly ConcurrentDictionary<MethodInfo, Lazy<ParameterMetadata>> ParameterMetadataCache = new();

    private ParameterMetadata GetParameterMetadata()
    {
        if (!ParameterMetadataCache.TryGetValue(MethodInfo, out Lazy<ParameterMetadata>? metadata))
        {
            ParameterInfo[] parametersInfo = ParameterTypes;
            metadata = ParameterMetadataCache.GetOrAdd(
                MethodInfo,
                _ => new Lazy<ParameterMetadata>(() => ParameterMetadata.Compute(parametersInfo), isThreadSafe: true));
        }

        return metadata.Value;
    }

    private readonly record struct ParameterMetadata(int ParamsParameterIndex, int RequiredParameterCount)
    {
        internal bool HasParams => ParamsParameterIndex >= 0;

        internal static ParameterMetadata Compute(ParameterInfo[] parametersInfo)
        {
            int requiredParameterCount = 0;
            int paramsParameterIndex = -1;
            for (int i = 0; i < parametersInfo.Length; i++)
            {
                ParameterInfo parameter = parametersInfo[i];

                // A params array parameter is not required and, when present, is always the last parameter.
                // Use IsDefined rather than GetCustomAttribute to avoid materializing the attribute instance.
                if (parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false))
                {
                    paramsParameterIndex = i;
                    break;
                }

                if (!parameter.IsOptional)
                {
                    requiredParameterCount++;
                }
            }

            return new ParameterMetadata(paramsParameterIndex, requiredParameterCount);
        }
    }

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
        => [.. PlatformServiceProvider.Instance.ReflectionOperations.GetAttributes<Attribute>(MethodInfo)];

    /// <summary>
    /// Gets all attributes of the test method.
    /// </summary>
    /// <typeparam name="TAttributeType">The type of the attributes.</typeparam>
    /// <returns>An array of the attributes.</returns>
    public TAttributeType[] GetAttributes<TAttributeType>()
        where TAttributeType : Attribute
        => [.. PlatformServiceProvider.Instance.ReflectionOperations.GetAttributes<TAttributeType>(MethodInfo)];

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
                result.SetOutputAndTraces(testContextImpl, TestContext);
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
        TestMethodAttribute testMethodAttribute = PlatformServiceProvider.Instance.ReflectionOperations.GetSingleAttributeOrDefault<TestMethodAttribute>(MethodInfo)!;

        // Get the derived TestMethod attribute from Extended TestClass Attribute
        // If the extended TestClass Attribute doesn't have extended TestMethod attribute then base class returns back the original testMethod Attribute
        return Parent.ClassAttribute.GetTestMethodAttribute(testMethodAttribute) ?? testMethodAttribute;
    }

    /// <summary>
    /// Resolves the retry attribute that applies to this test method, considering both
    /// method-level and class-level <see cref="RetryBaseAttribute"/> attributes.
    /// </summary>
    /// <remarks>
    /// A method-level retry attribute fully overrides any class-level retry attribute.
    /// Class-level retry attributes are always validated (even when the method has its own
    /// retry) so that misuse on the test class is reported regardless of method overrides.
    /// </remarks>
    /// <returns>
    /// The resolved <see cref="RetryBaseAttribute"/>, or <see langword="null"/> if neither
    /// the method nor the declaring class is decorated.
    /// </returns>
    private RetryBaseAttribute? GetRetryAttribute()
    {
        RetryBaseAttribute? methodRetry = GetSingleRetryAttribute(
            PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributesCached(MethodInfo),
            RetryAttributeScope.Method);

        // Always scan the class as well so a misuse there (multiple class-level retry
        // attributes) is reported even when the method has its own retry override.
        RetryBaseAttribute? classRetry = GetSingleRetryAttribute(
            PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributesCached(Parent.ClassType),
            RetryAttributeScope.Class);

        // Method-level retry fully overrides class-level retry when present.
        return methodRetry ?? classRetry;
    }

    /// <summary>
    /// Returns the single <see cref="RetryBaseAttribute"/> found in <paramref name="attributes"/>,
    /// or <see langword="null"/> if none is present.
    /// </summary>
    /// <param name="attributes">The attribute set to scan (method-level or class-level).</param>
    /// <param name="scope">Indicates whether <paramref name="attributes"/> comes from a method or a class; only used to pick the right error message when more than one retry attribute is found.</param>
    /// <exception cref="ObjectModel.TypeInspectionException">
    /// Thrown when <paramref name="attributes"/> contains more than one <see cref="RetryBaseAttribute"/>.
    /// </exception>
    private RetryBaseAttribute? GetSingleRetryAttribute(Attribute[] attributes, RetryAttributeScope scope)
    {
        RetryBaseAttribute? result = null;
        foreach (Attribute attribute in attributes)
        {
            if (attribute is RetryBaseAttribute retryAttribute)
            {
                if (result is not null)
                {
                    if (scope == RetryAttributeScope.Class)
                    {
                        ThrowMultipleClassAttributesException(nameof(RetryBaseAttribute));
                    }
                    else
                    {
                        ThrowMultipleAttributesException(nameof(RetryBaseAttribute));
                    }
                }

                result = retryAttribute;
            }
        }

        return result;
    }

    private enum RetryAttributeScope
    {
        Method,
        Class,
    }
}
