// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests;

/// <summary>
/// Regression tests for previously reported and fixed bugs.
/// </summary>
public class RegressionTests : TestContainer
{
    #region Issue #6458 / PR #6459 — SynchronizedStringBuilder thread safety

    public void SynchronizedStringBuilder_ConcurrentAppend_ShouldNotThrow()
    {
        var sb = new TestContextImplementation.SynchronizedStringBuilder();
        const int threadCount = 10;
        const int iterationsPerThread = 500;
        Exception? caughtException = null;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (int j = 0; j < iterationsPerThread; j++)
                    {
                        sb.Append($"T{threadId}-{j} ");
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        caughtException.Should().BeNull("concurrent Append calls should not throw");
        string result = sb.ToString();
        result.Should().NotBeNullOrEmpty();
    }

    public void SynchronizedStringBuilder_ConcurrentAppendLine_ShouldContainAllMessages()
    {
        var sb = new TestContextImplementation.SynchronizedStringBuilder();
        const int threadCount = 8;
        const int iterationsPerThread = 100;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < iterationsPerThread; j++)
                {
                    sb.AppendLine($"Msg-{threadId}-{j}");
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        string result = sb.ToString();
        for (int i = 0; i < threadCount; i++)
        {
            result.Should().Contain($"Msg-{i}-0");
            result.Should().Contain($"Msg-{i}-{iterationsPerThread - 1}");
        }
    }

    public void SynchronizedStringBuilder_ConcurrentAppendAndClear_ShouldNotThrow()
    {
        var sb = new TestContextImplementation.SynchronizedStringBuilder();
        Exception? caughtException = null;

        var writer = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    sb.Append("data");
                    sb.AppendLine("line");
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref caughtException, ex, null);
            }
        });

        var clearer = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    sb.Clear();
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref caughtException, ex, null);
            }
        });

        var reader = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    _ = sb.ToString();
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref caughtException, ex, null);
            }
        });

        writer.Start();
        clearer.Start();
        reader.Start();

        writer.Join();
        clearer.Join();
        reader.Join();

        caughtException.Should().BeNull("concurrent Append, Clear, and ToString should not throw");
    }

    public void SynchronizedStringBuilder_ConcurrentAppendCharOverload_ShouldNotThrow()
    {
        var sb = new TestContextImplementation.SynchronizedStringBuilder();
        Exception? caughtException = null;
        const int threadCount = 10;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (int j = 0; j < 500; j++)
                    {
                        sb.Append('x');
                        sb.Append("hello".ToCharArray(), 0, 5);
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        caughtException.Should().BeNull("concurrent char and char[] Append should not throw");
    }

    #endregion

    #region Issue #6458 / PR #6459 — TestContextImplementation concurrent message writes

    public void TestContextImplementation_ConcurrentWriteLine_ShouldNotThrow()
    {
        var testMethod = new Mock<ITestMethod>();
        testMethod.Setup(tm => tm.FullClassName).Returns("TestClass");
        testMethod.Setup(tm => tm.Name).Returns("TestMethod");
        var properties = new Dictionary<string, object?>();
        var testContext = new TestContextImplementation(testMethod.Object, null, properties, null, null);

        const int threadCount = 10;
        const int iterationsPerThread = 200;
        Exception? caughtException = null;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (int j = 0; j < iterationsPerThread; j++)
                    {
                        testContext.WriteLine($"Thread {threadId} message {j}");
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        caughtException.Should().BeNull("concurrent WriteLine calls should not throw");

        string? messages = testContext.GetDiagnosticMessages();
        messages.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Issue #5467 / PR #5498 — FixtureMethodRunner: real exception preserved, not AggregateException

    public async Task FixtureMethodRunner_WhenActionThrowsWithTimeout_ShouldPropagateRealException()
    {
        var expectedException = new InvalidOperationException("Original fixture exception");
        var cts = new CancellationTokenSource();
        MethodInfo methodInfo = typeof(RegressionTestHelpers).GetMethod(nameof(RegressionTestHelpers.DummyFixtureMethod))!;

        // RunWithTimeoutAndCancellationAsync with a non-cooperative timeout causes execution on
        // a separate thread, which is where the AggregateException wrapping previously occurred.
        Func<Task> action = async () =>
        {
            await FixtureMethodRunner.RunWithTimeoutAndCancellationAsync(
                () => new SynchronizationContextPreservingTask(Task.FromException(expectedException)),
                cts,
                TimeoutInfo.FromTimeout(30000),
                methodInfo,
                executionContext: null,
                "Method {0}.{1} was canceled",
                "Method {0}.{1} timed out after {2}ms");
        };

        // The fix captures the real exception inside the Task and re-throws it,
        // instead of letting Task.Wait() wrap it in AggregateException.
        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Original fixture exception");
    }

    #endregion

    #region Issue #5249 / PR #5293 — TestProperty resolved from actual test class

    public void TestProperty_ShouldResolveFromDerivedClass_NotBaseOnly()
    {
        ReflectHelper.Instance.ClearCache();

        // The fix ensures TestProperty attributes are resolved from the actual
        // (derived) test class, not just the base class.
        Trait[] traits = [.. ReflectHelper.Instance.GetTestPropertiesAsTraits(
            typeof(DerivedTestClassForPropertyTest).GetMethod(nameof(DerivedTestClassForPropertyTest.TestMethodWithProperty))!)];

        // Should include properties from both the derived class and the method itself
        traits.Should().Contain(t => t.Name == "DerivedClassProp" && t.Value == "DerivedValue");
        traits.Should().Contain(t => t.Name == "MethodProp" && t.Value == "MethodValue");
    }

    public void TestProperty_ShouldIncludeBaseClassProperties_WhenDerivedClassHasProperties()
    {
        ReflectHelper.Instance.ClearCache();

        Trait[] traits = [.. ReflectHelper.Instance.GetTestPropertiesAsTraits(
            typeof(DerivedTestClassForPropertyTest).GetMethod(nameof(DerivedTestClassForPropertyTest.TestMethodWithProperty))!)];

        // Base class properties should also be present
        traits.Should().Contain(t => t.Name == "BaseClassProp" && t.Value == "BaseValue");
    }

    #endregion

    #region Issue #3953 / PR #3958 — TestRunCancellationToken concurrent Cancel()

    public void TestRunCancellationToken_ConcurrentCancel_ShouldNotThrow()
    {
        var token = new TestRunCancellationToken();
        const int threadCount = 20;
        Exception? caughtException = null;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    token.Cancel();
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        caughtException.Should().BeNull("concurrent Cancel() calls should not throw");
        token.Canceled.Should().BeTrue();
    }

    public void TestRunCancellationToken_RegisterAndCancelFromDifferentThreads_ShouldNotThrow()
    {
        var token = new TestRunCancellationToken();
        int callbackCount = 0;
        Exception? caughtException = null;

        var registerThread = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    token.Register(_ => Interlocked.Increment(ref callbackCount), null);
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref caughtException, ex, null);
            }
        });

        var cancelThread = new Thread(() =>
        {
            try
            {
                Thread.Sleep(10);
                token.Cancel();
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref caughtException, ex, null);
            }
        });

        registerThread.Start();
        cancelThread.Start();

        registerThread.Join();
        cancelThread.Join();

        caughtException.Should().BeNull("Register and Cancel from different threads should not throw");
        token.Canceled.Should().BeTrue();
    }

    public void TestRunCancellationToken_CancelShouldSetCanceledProperty()
    {
        var token = new TestRunCancellationToken();

        token.Canceled.Should().BeFalse();

        token.Cancel();

        token.Canceled.Should().BeTrue();
    }

    public void TestRunCancellationToken_ThrowIfCancellationRequested_ShouldThrowAfterCancel()
    {
        var token = new TestRunCancellationToken();
        token.Cancel();

        Action action = () => token.ThrowIfCancellationRequested();

        action.Should().Throw<OperationCanceledException>();
    }

    public void TestRunCancellationToken_RegisteredCallback_ShouldBeInvokedOnCancel()
    {
        var token = new TestRunCancellationToken();
        bool callbackInvoked = false;

        token.Register(_ => callbackInvoked = true, null);
        token.Cancel();

        callbackInvoked.Should().BeTrue("callback should be invoked when Cancel is called");
    }

    public void TestRunCancellationToken_MultipleCancelCalls_ShouldOnlyTriggerCancellationOnce()
    {
        var token = new TestRunCancellationToken();
        int callbackCount = 0;

        token.Register(_ => Interlocked.Increment(ref callbackCount), null);

        // Cancel multiple times
        token.Cancel();
        token.Cancel();
        token.Cancel();

        // CancellationTokenSource.Cancel() is idempotent, so the callback fires once
        callbackCount.Should().Be(1, "callback should be invoked only once even with multiple Cancel calls");
    }

    #endregion

    #region Issue #522 / PR #3334 — TestCleanup should run after timeout

    public async Task TestMethodInfo_WhenTestThrowsException_ShouldStillInvokeCleanup()
    {
        // Regression test for Issue #522: TestCleanup was not invoked after timeout/failure.
        // We verify that cleanup is always called even when the test method fails,
        // which is the fundamental behavior that the fix ensures.
        var testablePlatformServiceProvider = new Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations.TestablePlatformServiceProvider();

        try
        {
            testablePlatformServiceProvider.MockThreadOperations.
                Setup(tho => tho.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).
                Returns(true).
                Callback((Action a, int timeout, CancellationToken token) => a.Invoke());

            PlatformServiceProvider.Instance = testablePlatformServiceProvider;

            ConstructorInfo constructorInfo = typeof(DummyTestClassForCleanupAfterTimeout).GetConstructor([])!;
            MethodInfo testMethodInfo = typeof(DummyTestClassForCleanupAfterTimeout).GetMethod(nameof(DummyTestClassForCleanupAfterTimeout.ThrowingTestMethod))!;
            MethodInfo cleanupMethodInfo = typeof(DummyTestClassForCleanupAfterTimeout).GetMethod(nameof(DummyTestClassForCleanupAfterTimeout.Cleanup))!;
            var classAttribute = new TestClassAttribute();
            var testAssemblyInfo = new TestAssemblyInfo(typeof(DummyTestClassForCleanupAfterTimeout).Assembly);
            var testClassInfo = new TestClassInfo(typeof(DummyTestClassForCleanupAfterTimeout), constructorInfo, true, classAttribute, testAssemblyInfo);
            testClassInfo.TestCleanupMethod = cleanupMethodInfo;

            var testMethod = new TestMethod("ThrowingTestMethod", typeof(DummyTestClassForCleanupAfterTimeout).FullName!, "TestAssembly", displayName: null);
            var testContextImpl = new TestContextImplementation(testMethod, null, new Dictionary<string, object?>(), null, null);
            using IDisposable scopedDisposable = TestContextImplementation.SetCurrentTestContext(testContextImpl);

            var method = new TestMethodInfo(testMethodInfo, testClassInfo)
            {
                TimeoutInfo = TimeoutInfo.FromTimeout(3600 * 1000),
                Executor = new TestMethodAttribute(),
            };

            DummyTestClassForCleanupAfterTimeout.CleanupCalled = false;

            Microsoft.VisualStudio.TestTools.UnitTesting.TestResult result = await method.InvokeAsync(null);

            result.Outcome.Should().Be(UnitTestOutcome.Failed, "test should have failed");
            DummyTestClassForCleanupAfterTimeout.CleanupCalled.Should().BeTrue(
                "TestCleanup should be invoked even after test failure (regression for issue #522)");
        }
        finally
        {
            PlatformServiceProvider.Instance = null;
        }
    }

    #endregion

    #region Issue #1063 / PR #1068 — Parallel output write conflicts

    public void TestContextImplementation_ConcurrentConsoleOutAndErr_ShouldNotThrow()
    {
        var testMethod = new Mock<ITestMethod>();
        testMethod.Setup(tm => tm.FullClassName).Returns("TestClass");
        testMethod.Setup(tm => tm.Name).Returns("TestMethod");
        var properties = new Dictionary<string, object?>();
        var testContext = new TestContextImplementation(testMethod.Object, null, properties, new Mock<IMessageLogger>().Object, null);

        const int threadCount = 10;
        Exception? caughtException = null;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (int j = 0; j < 500; j++)
                    {
                        testContext.WriteConsoleOut(new string('a', 1000));
                        testContext.WriteConsoleErr(new string('b', 1000));
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        // Read while writing — this is where the original race condition manifested
        for (int i = 0; i < 50; i++)
        {
            _ = testContext.GetOut();
            _ = testContext.GetErr();
            Thread.Sleep(1);
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        caughtException.Should().BeNull("concurrent console output writes should not throw");

        string? outResult = testContext.GetOut();
        string? errResult = testContext.GetErr();
        outResult.Should().NotBeNullOrEmpty();
        errResult.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Issue #1053 / PR #1055 — ExceptionHelper concurrent access

    public void ExceptionHelper_GetStackTraceInformation_ConcurrentAccess_ShouldNotThrow()
    {
        // Throw and catch to populate stack trace (StackTraceInformation requires non-empty trace)
        Exception exception;
        try
        {
            throw new InvalidOperationException(
                "Test exception",
                new ArgumentException("Inner exception"));
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        const int threadCount = 20;
        Exception? caughtException = null;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        StackTraceInformation? info = exception.GetStackTraceInformation();
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        caughtException.Should().BeNull("concurrent GetStackTraceInformation calls should not throw");
    }

    public void ExceptionHelper_GetFormattedExceptionMessage_ConcurrentAccess_ShouldNotThrow()
    {
        var exception = new InvalidOperationException(
            "Test exception",
            new ArgumentException("Inner exception"));

        const int threadCount = 20;
        Exception? caughtException = null;

        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                try
                {
                    for (int j = 0; j < 100; j++)
                    {
                        string message = exception.GetFormattedExceptionMessage();
                        message.Should().Contain("Test exception");
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.CompareExchange(ref caughtException, ex, null);
                }
            });
        }

        foreach (Thread t in threads)
        {
            t.Start();
        }

        foreach (Thread t in threads)
        {
            t.Join();
        }

        caughtException.Should().BeNull("concurrent GetFormattedExceptionMessage calls should not throw");
    }

    #endregion

    #region Issue #1645 / PR #1669 — Assembly resolution

    public void ExceptionExtensions_GetRealException_ShouldUnwrapTargetInvocationException()
    {
        var innerException = new InvalidOperationException("Real exception from assembly init");
        var wrappedException = new TargetInvocationException(innerException);

        Exception result = wrappedException.GetRealException();

        result.Should().BeSameAs(innerException);
        result.Should().BeOfType<InvalidOperationException>();
        result.Message.Should().Be("Real exception from assembly init");
    }

    public void ExceptionExtensions_GetRealException_ShouldUnwrapTypeInitializationException()
    {
        var innerException = new InvalidOperationException("Real exception from type init");
        var wrappedException = new TypeInitializationException("SomeType", innerException);

        Exception result = wrappedException.GetRealException();

        result.Should().BeSameAs(innerException);
    }

    public void ExceptionExtensions_GetRealException_ShouldUnwrapNestedWrappingExceptions()
    {
        var realException = new InvalidOperationException("Deep exception");
        var inner = new TargetInvocationException(realException);
        var outer = new TypeInitializationException("SomeType", inner);

        Exception result = outer.GetRealException();

        result.Should().BeSameAs(realException);
    }

    #endregion

    #region Issue #1493 / PR #1502 — Deployment items loading

    public void ExceptionExtensions_GetExceptionMessage_ShouldIncludeInnerExceptions()
    {
        // When deployment items fail to load, the exception chain is important for diagnosis.
        var innerMost = new FileNotFoundException("Could not load CoreUtilities.dll");
        var inner = new InvalidOperationException("Deployment item resolution failed", innerMost);
        var outer = new TargetInvocationException("Invocation failed", inner);

        string message = outer.GetExceptionMessage();

        message.Should().Contain("Invocation failed");
        message.Should().Contain("Deployment item resolution failed");
        message.Should().Contain("Could not load CoreUtilities.dll");
    }

    #endregion

    #region Issue #1437 / PR #1443 — Assembly resolver registration

    public void TestRunCancellationToken_WithOriginalCancellationToken_ShouldRespectOriginalToken()
    {
        // Validates that TestRunCancellationToken properly bridges with
        // the original CancellationToken, which is important for proper
        // assembly resolver registration across app domains.
        var cts = new CancellationTokenSource();
        var token = new TestRunCancellationToken(cts.Token);

        cts.Cancel();

        Action action = () => token.ThrowIfCancellationRequested();
        action.Should().Throw<OperationCanceledException>();
    }

    #endregion

    #region Test helpers and dummy classes

    [TestClass]
    [TestProperty("BaseClassProp", "BaseValue")]
    internal class BaseTestClassForPropertyTest
    {
        [TestMethod]
        [TestProperty("MethodProp", "MethodValue")]
        public virtual void TestMethodWithProperty()
        {
        }
    }

    [TestClass]
    [TestProperty("DerivedClassProp", "DerivedValue")]
    internal class DerivedTestClassForPropertyTest : BaseTestClassForPropertyTest
    {
        [TestMethod]
        public override void TestMethodWithProperty() => base.TestMethodWithProperty();
    }

    public class DummyTestClassForCleanupAfterTimeout
    {
        public static bool CleanupCalled { get; set; }

        public TestContext TestContext
        {
            get => throw new NotImplementedException();
            set { }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void ThrowingTestMethod()
        {
            throw new InvalidOperationException("Test method failed");
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public void Cleanup()
        {
            CleanupCalled = true;
        }
    }

    internal static class RegressionTestHelpers
    {
        public static void DummyFixtureMethod()
        {
        }
    }

    #endregion
}
