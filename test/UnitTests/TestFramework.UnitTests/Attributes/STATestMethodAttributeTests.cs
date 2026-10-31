// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using AwesomeAssertions;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public sealed class STATestMethodAttributeTests : TestContainer
{
    public void ExecuteAsync_DefaultAttribute_RunsBaseInvocationOnStaAndReturnsOriginalResult()
    {
        if (!SupportsSta)
        {
            VerifyNonWindowsRejection(useContext: false);
            return;
        }

        var result = new TestResult { Outcome = UnitTestOutcome.Passed, DisplayName = "STA result" };
        var method = new Mock<ITestMethod>();
        int callerThread = 0;
        int invocationThread = 0;
        ApartmentState apartment = ApartmentState.Unknown;
        string? threadName = null;
        object[]? passedArguments = [new object()];
        method.Setup(test => test.InvokeAsync(It.IsAny<object[]?>())).Returns((object[]? arguments) =>
        {
            passedArguments = arguments;
            invocationThread = Environment.CurrentManagedThreadId;
            apartment = Thread.CurrentThread.GetApartmentState();
            threadName = Thread.CurrentThread.Name;
            return Task.FromResult(result);
        });
        var attribute = new STATestMethodAttribute("fixture.cs", 37);
        TestResult[] actual = RunInApartment(ApartmentState.MTA, () =>
        {
            callerThread = Environment.CurrentManagedThreadId;
            return attribute.ExecuteAsync(method.Object).GetAwaiter().GetResult();
        });

        actual.Should().ContainSingle().Which.Should().BeSameAs(result);
        apartment.Should().Be(ApartmentState.STA);
        invocationThread.Should().NotBe(callerThread);
        threadName.Should().Be("STATestMethodAttribute thread");
        passedArguments.Should().BeNull();
        attribute.DeclaringFilePath.Should().Be("fixture.cs");
        attribute.DeclaringLineNumber.Should().Be(37);
        method.Verify(test => test.InvokeAsync(null), Times.Once);
        method.VerifyNoOtherCalls();
    }

    public void ExecuteAsync_ClassWrapper_PreservesSourceAndReusesAlreadyStaThread()
    {
        var method = new Mock<ITestMethod>();
        TestResult[] expected = [new() { Outcome = UnitTestOutcome.Passed, DisplayName = "wrapped result" }];
        int callerThread = 0;
        int invocationThread = 0;
        var wrapped = new CallbackTestMethodAttribute(
            _ =>
            {
                invocationThread = Environment.CurrentManagedThreadId;
                return Task.FromResult(expected);
            }, "wrapped.cs", 123);
        var attribute = (STATestMethodAttribute)new STATestClassAttribute().GetTestMethodAttribute(wrapped)!;
        attribute.DeclaringFilePath.Should().Be("wrapped.cs");
        attribute.DeclaringLineNumber.Should().Be(123);
        if (!SupportsSta)
        {
            Action rejected = () => attribute.ExecuteAsync(method.Object).GetAwaiter().GetResult();
            rejected.Should().ThrowExactly<PlatformNotSupportedException>().WithMessage(NonWindowsMessage);
            wrapped.Calls.Should().Be(0);
            return;
        }

        TestResult[] actual = RunInApartment(ApartmentState.STA, () =>
        {
            callerThread = Environment.CurrentManagedThreadId;
            return attribute.ExecuteAsync(method.Object).GetAwaiter().GetResult();
        });
        actual.Should().BeSameAs(expected);
        invocationThread.Should().Be(callerThread);
        wrapped.Calls.Should().Be(1);
        wrapped.LastMethod.Should().BeSameAs(method.Object);
        method.VerifyNoOtherCalls();
    }

    public void ExecuteAsync_StaSynchronizationContext_PreservesAsyncThreadAndRestoresCaller()
    {
        if (!SupportsSta)
        {
            VerifyNonWindowsRejection(useContext: true);
            return;
        }

        var method = new Mock<ITestMethod>();
        TestResult[] expected = [new() { Outcome = UnitTestOutcome.Passed, DisplayName = "async result" }];
        int callerThread = 0;
        int beforeThread = 0;
        int afterThread = 0;
        ApartmentState beforeApartment = ApartmentState.Unknown;
        ApartmentState afterApartment = ApartmentState.Unknown;
        SynchronizationContext? beforeContext = null;
        SynchronizationContext? afterContext = null;
        bool callerRestored = false;
        var wrapped = new CallbackTestMethodAttribute(async _ =>
        {
            beforeThread = Environment.CurrentManagedThreadId;
            beforeApartment = Thread.CurrentThread.GetApartmentState();
            beforeContext = SynchronizationContext.Current;
            // This posts to the same worker's queue, so the core is guaranteed incomplete
            // until its current callback returns. It is not a wall-clock race.
            await Task.Yield();
            afterThread = Environment.CurrentManagedThreadId;
            afterApartment = Thread.CurrentThread.GetApartmentState();
            afterContext = SynchronizationContext.Current;
            return expected;
        });
        var attribute = new STATestMethodAttribute(wrapped) { UseSTASynchronizationContext = true };
        TestResult[] actual = RunInApartment(ApartmentState.MTA, () =>
        {
            callerThread = Environment.CurrentManagedThreadId;
            SynchronizationContext? previous = SynchronizationContext.Current;
            var sentinel = new SynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sentinel);
            try
            {
                TestResult[] results = attribute.ExecuteAsync(method.Object).GetAwaiter().GetResult();
                callerRestored = ReferenceEquals(SynchronizationContext.Current, sentinel);
                return results;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        });

        actual.Should().BeSameAs(expected);
        beforeThread.Should().NotBe(callerThread).And.Be(afterThread);
        beforeApartment.Should().Be(ApartmentState.STA);
        afterApartment.Should().Be(ApartmentState.STA);
        beforeContext.Should().BeOfType<SingleThreadedSTASynchronizationContext>();
        afterContext.Should().BeSameAs(beforeContext);
        callerRestored.Should().BeTrue();
        wrapped.Calls.Should().Be(1);
        wrapped.LastMethod.Should().BeSameAs(method.Object);
        Action postAfterCompletion = () => ((SingleThreadedSTASynchronizationContext)beforeContext!).Post(_ => { }, null);
        postAfterCompletion.Should().ThrowExactly<ObjectDisposedException>();
        method.VerifyNoOtherCalls();
    }

    public void ExecuteAsync_StaSynchronizationContext_FailurePropagatesAndDisposesQueue()
    {
        if (!SupportsSta)
        {
            VerifyNonWindowsRejection(useContext: true);
            return;
        }

        var original = new InvalidOperationException("async execution contract");
        var method = new Mock<ITestMethod>();
        SynchronizationContext? context = null;
        var wrapped = new CallbackTestMethodAttribute(async _ =>
        {
            context = SynchronizationContext.Current;
            await Task.Yield();
            throw original;
        });
        var attribute = new STATestMethodAttribute(wrapped) { UseSTASynchronizationContext = true };
        Action action = () => RunInApartment(
            ApartmentState.MTA,
            () => attribute.ExecuteAsync(method.Object).GetAwaiter().GetResult());
        action.Should().ThrowExactly<InvalidOperationException>().Which.Should().BeSameAs(original);
        wrapped.Calls.Should().Be(1);
        context.Should().BeOfType<SingleThreadedSTASynchronizationContext>();
        Action postAfterFailure = () => ((SingleThreadedSTASynchronizationContext)context!).Post(_ => { }, null);
        postAfterFailure.Should().ThrowExactly<ObjectDisposedException>();
        method.VerifyNoOtherCalls();
    }

    public void ExecuteAsync_NonWindows_RejectsBothStaModesBeforeInvokingTest()
    {
        if (!SupportsSta)
        {
            VerifyNonWindowsRejection(useContext: false);
            VerifyNonWindowsRejection(useContext: true);
            return;
        }

        var result = new TestResult { Outcome = UnitTestOutcome.Passed };
        var method = new Mock<ITestMethod>();
        method.Setup(test => test.InvokeAsync(null)).ReturnsAsync(result);
        TestResult[] actual = RunInApartment(
            ApartmentState.STA,
            () => new STATestMethodAttribute().ExecuteAsync(method.Object).GetAwaiter().GetResult());
        actual.Should().ContainSingle().Which.Should().BeSameAs(result);
        method.Verify(test => test.InvokeAsync(null), Times.Once);
    }

    private const string NonWindowsMessage =
        "[STATestMethod] is not supported on non-Windows platforms. STA (Single Threaded Apartment) is a Windows-only COM threading concept. Use [OSCondition(OperatingSystems.Windows)] to skip this test on non-Windows platforms.";

#if NET5_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatformGuard("windows")]
#endif
    private static bool SupportsSta
#if NETFRAMEWORK
        => true;
#else
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif

    private static void VerifyNonWindowsRejection(bool useContext)
    {
        var method = new Mock<ITestMethod>();
        var attribute = new STATestMethodAttribute { UseSTASynchronizationContext = useContext };
        Action action = () => attribute.ExecuteAsync(method.Object).GetAwaiter().GetResult();
        if (useContext)
        {
            action.Should().ThrowExactly<NotSupportedException>()
                .WithMessage("SingleThreadedSTASynchronizationContext is only supported on Windows.");
        }
        else
        {
            action.Should().ThrowExactly<PlatformNotSupportedException>().WithMessage(NonWindowsMessage);
        }

        method.VerifyNoOtherCalls();
    }

    private static T RunInApartment<T>(ApartmentState apartment, Func<T> callback)
    {
#if !NETFRAMEWORK
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("The apartment test helper requires Windows.");
        }
#endif

        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = callback();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });
        thread.SetApartmentState(apartment);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return result!;
    }

    private sealed class CallbackTestMethodAttribute : TestMethodAttribute
    {
        private readonly Func<ITestMethod, Task<TestResult[]>> _callback;

        public CallbackTestMethodAttribute(
            Func<ITestMethod, Task<TestResult[]>> callback,
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = -1)
            : base(callerFilePath, callerLineNumber)
            => _callback = callback;

        public int Calls { get; private set; }

        public ITestMethod? LastMethod { get; private set; }

        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            Calls++;
            LastMethod = testMethod;
            return _callback(testMethod);
        }
    }
}
