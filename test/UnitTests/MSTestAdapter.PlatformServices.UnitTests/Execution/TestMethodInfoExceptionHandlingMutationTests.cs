// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
#if !NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodInfoExceptionHandlingMutationTests : TestContainer
{
    private const string ClassName = "Contoso.Tests.SampleClass";
    private const string MethodName = "SampleMethod";

#if DEBUG && !NETFRAMEWORK
    public void HandleMethodExceptionAssertsThatTheExceptionIsProvided()
    {
        using var harness = new ExceptionHarness();
        InvalidOperationException realException = ThrowAndCatch(new InvalidOperationException("boom"));
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        TestFailedException result = harness.HandleMethodException(null, realException, classInstanceCreated: true);

        assertions.Should().Equal("exception should not be null.");
        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        result.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestMethodThrows, ClassName, MethodName, realException.GetFormattedExceptionMessage()));
    }

    public void ConstructorAssertsThatTheTestMethodAndTheParentAreProvided()
    {
        using var harness = new ExceptionHarness();

        // A loose mock reports no attributes and never dereferences the (null) member it is asked about.
        harness.Services.MockReflectionOperations = new();
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        _ = new TestMethodInfo(harness.Method, harness.ClassInfo);
        assertions.Should().BeEmpty(because: "a valid method and parent satisfy every constructor assertion");

        _ = new TestMethodInfo(null!, harness.ClassInfo);
        assertions.Should().Equal("TestMethod should not be null", "TestMethod should be non-null");

        assertions.Clear();
        Action constructWithoutParent = () => _ = new TestMethodInfo(harness.Method, null!);
        constructWithoutParent.Should().Throw<NullReferenceException>(because: "the class type of the parent is needed to resolve the retry attribute");
        assertions.Should().Equal("Parent should not be null");
    }
#endif

    public void HandleMethodExceptionReportsAnErrorWhenATargetInvocationExceptionHasNoInnerException()
    {
        using var harness = new ExceptionHarness();

        // The exception is thrown so that it has a stack trace: without the dedicated handling, the generic path would
        // report a regular failure instead of failing fast on the missing stack trace.
        TargetInvocationException exception = ThrowAndCatch(new TargetInvocationException(null));

        TestFailedException result = harness.HandleMethodException(exception, exception, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.Error);
        result.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_FailedToGetTestMethodException, ClassName, MethodName));
        result.StackTraceInformation.Should().BeNull();
    }

    public void HandleMethodExceptionReportsAFailureWhenATargetInvocationExceptionHasAnInnerException()
    {
        using var harness = new ExceptionHarness();
        InvalidOperationException inner = ThrowAndCatch(new InvalidOperationException("boom"));
        var exception = new TargetInvocationException(inner);

        TestFailedException result = harness.HandleMethodException(exception, inner, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        result.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestMethodThrows, ClassName, MethodName, inner.GetFormattedExceptionMessage()));
        result.InnerException.Should().BeSameAs(inner);
    }

    public void HandleMethodExceptionReportsTheStackTraceOfTheRealException()
    {
        using var harness = new ExceptionHarness();
        InvalidOperationException exception = ThrowAndCatch(new InvalidOperationException("boom"));

        TestFailedException result = harness.HandleMethodException(exception, exception, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        result.StackTraceInformation.Should().NotBeNull();
        result.StackTraceInformation!.ErrorStackTrace.Should().Contain(exception.StackTrace!);
    }

    public void HandleMethodExceptionDoesNotReportTheStackTraceOfAThreadAbortException()
    {
        using var harness = new ExceptionHarness();
        ThreadAbortException exception = ThrowAndCatch(new ThreadAbortException());

        TestFailedException result = harness.HandleMethodException(exception, exception, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        result.StackTraceInformation.Should().BeNull(because: "the stack of a thread abort only holds reflection frames that are meaningless to the user");
        result.InnerException.Should().BeSameAs(exception);
    }

#if !NETFRAMEWORK
    public void HandleMethodExceptionReportsNotFoundWhenHotReloadIsOnAndTheFirstFrameIsTheTestMethod()
    {
        using var harness = new ExceptionHarness();
        using var hotReload = new HotReloadScope(enabled: true);
        var exception = new StackTraceMissingMethodException(
            "Method not found: 'Void SampleMethod()'.",
            $"   at {ClassName}.{MethodName}(){Environment.NewLine}   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs()");

        TestFailedException result = harness.HandleMethodException(exception, exception, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.NotFound);
        result.Message.Should().Be(exception.Message);
        result.InnerException.Should().BeSameAs(exception);
    }

    public void HandleMethodExceptionReportsAFailureWhenOnlyALaterFrameOfAMissingMethodExceptionIsTheTestMethod()
    {
        using var harness = new ExceptionHarness();
        using var hotReload = new HotReloadScope(enabled: true);
        var exception = new StackTraceMissingMethodException(
            "Method not found: 'Void Other()'.",
            $"   at Contoso.Tests.Other.Helper(){Environment.NewLine}   at {ClassName}.{MethodName}()");

        TestFailedException result = harness.HandleMethodException(exception, exception, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.Failed, because: "the test method is not the one that is missing, it merely calls a missing method");
        result.Message.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.UTA_TestMethodThrows, ClassName, MethodName, exception.GetFormattedExceptionMessage()));
    }

    public void HandleMethodExceptionReportsAFailureWhenTheStackTraceOfAMissingMethodExceptionHasASingleFrame()
    {
        using var harness = new ExceptionHarness();
        using var hotReload = new HotReloadScope(enabled: true);
        var exception = new StackTraceMissingMethodException("Method not found: 'Void SampleMethod()'.", $"   at {ClassName}.{MethodName}()");

        TestFailedException result = harness.HandleMethodException(exception, exception, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.Failed, because: "without a line break there is no first line to inspect");
        result.InnerException.Should().BeSameAs(exception);
    }

    public void HandleMethodExceptionReportsAFailureForAMissingMethodExceptionWhenHotReloadIsOff()
    {
        using var harness = new ExceptionHarness();
        using var hotReload = new HotReloadScope(enabled: false);
        var exception = new StackTraceMissingMethodException(
            "Method not found: 'Void SampleMethod()'.",
            $"   at {ClassName}.{MethodName}(){Environment.NewLine}   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs()");

        TestFailedException result = harness.HandleMethodException(exception, exception, classInstanceCreated: true);

        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        result.InnerException.Should().BeSameAs(exception);
    }
#endif

    private static T ThrowAndCatch<T>(T exception)
        where T : Exception
    {
        Action throwing = () => throw exception;
        return throwing.Should().Throw<T>().Which;
    }

    private sealed class ExceptionHarness : IDisposable
    {
        private static readonly MethodInfo HandleMethodExceptionMethod = typeof(TestMethodInfo).GetMethod("HandleMethodException", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly IPlatformServiceProvider _previousProvider;

        public ExceptionHarness()
        {
            _previousProvider = PlatformServiceProvider.Instance;
            Services = new TestablePlatformServiceProvider();
            Services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = Services;

            AssemblyInfo = new TestAssemblyInfo(typeof(ExceptionFixture).Assembly);
            ClassInfo = new TestClassInfo(typeof(ExceptionFixture), typeof(ExceptionFixture).GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), AssemblyInfo);
            Method = typeof(ExceptionFixture).GetMethod(nameof(ExceptionFixture.TestMethod))!;
            Subject = new TestMethodInfo(Method, ClassInfo);
        }

        public TestablePlatformServiceProvider Services { get; }

        public TestAssemblyInfo AssemblyInfo { get; }

        public TestClassInfo ClassInfo { get; }

        public MethodInfo Method { get; }

        public TestMethodInfo Subject { get; }

        public TestFailedException HandleMethodException(Exception? exception, Exception realException, bool classInstanceCreated)
            => (TestFailedException)HandleMethodExceptionMethod.Invoke(Subject, [exception, realException, ClassName, MethodName, classInstanceCreated])!;

        public void Dispose()
            => PlatformServiceProvider.Instance = _previousProvider;
    }

    private sealed class ExceptionFixture
    {
        public void TestMethod()
        {
        }
    }

    private sealed class ThreadAbortException : Exception
    {
        public ThreadAbortException()
            : base("The thread was aborted.")
        {
        }
    }

#if !NETFRAMEWORK
    private sealed class StackTraceMissingMethodException : MissingMethodException
    {
        public StackTraceMissingMethodException(string message, string stackTrace)
            : base(message)
            => StackTrace = stackTrace;

        public override string StackTrace { get; }
    }

    private sealed class HotReloadScope : IDisposable
    {
        private static readonly FieldInfo LazyField = typeof(RuntimeContext).GetField("IsHotReloadEnabledLazy", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("RuntimeContext.IsHotReloadEnabledLazy was not found.");

        // The static field is readonly, but the value cached by an evaluated Lazy<bool> can be replaced.
        private static readonly FieldInfo ValueField = typeof(Lazy<bool>).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).SingleOrDefault(field => field.FieldType == typeof(bool))
            ?? throw new InvalidOperationException("The cached value of Lazy<bool> was not found.");

        private readonly Lazy<bool> _lazy;
        private readonly bool _previousValue;

        public HotReloadScope(bool enabled)
        {
            _lazy = (Lazy<bool>)LazyField.GetValue(null)!;
            _previousValue = _lazy.Value;
            ValueField.SetValue(_lazy, enabled);
        }

        public void Dispose()
            => ValueField.SetValue(_lazy, _previousValue);
    }
#endif
}
