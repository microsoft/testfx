// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WIN_UI
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Extensions;

public sealed class WinUIExtensionTests : TestContainer
{
    private const string ChildModeVariable = "TESTFX_EXTENSIONS_NATIVE_STARTUP_CHILD";
    private static readonly TimeSpan RendezvousLimit = TimeSpan.FromSeconds(30);

    public void TargetAttributeValidatesAndPreservesApplicationType()
    {
        new WinUITestTargetAttribute(typeof(Application)).ApplicationType.Should().Be(typeof(Application));
        Action nullType = () => _ = new WinUITestTargetAttribute(null!);
        nullType.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("applicationType");
        Action unrelated = () => _ = new WinUITestTargetAttribute(typeof(string));
        unrelated.Should().ThrowExactly<ArgumentException>().WithMessage("*Microsoft.UI.Xaml.Application*")
            .Which.ParamName.Should().Be("applicationType");
    }

    public void UnpackagedProcessHasNoPackageIdentityOrPath()
    {
        Type model = typeof(UITestMethodAttribute).Assembly.GetType("Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer.AppModel", true)!;
        model.GetMethod("IsPackagedProcess")!.Invoke(null, null).Should().Be(false);
        model.GetMethod("GetCurrentPackagePath")!.Invoke(null, null).Should().BeNull();
    }

    public void StartupWaitCompletesOrReportsThreadExitAndTimeout()
    {
        MethodInfo wait = typeof(UITestMethodAttribute).GetMethod("WaitForApplicationStartup", BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeof(int));
        var completed = new TaskCompletionSource<int>();
        completed.SetResult(23);
        int polls = 0;
        Func<TimeSpan, bool> alive = _ =>
        {
            polls++;
            return false;
        };
        wait.Invoke(null, [completed, alive, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100)]);
        completed.Task.GetAwaiter().GetResult().Should().Be(23);
        polls.Should().Be(0);

        var dying = new TaskCompletionSource<int>();
        Func<TimeSpan, bool> exited = interval =>
        {
            interval.Should().Be(TimeSpan.FromMilliseconds(100));
            return true;
        };
        wait.Invoke(null, [dying, exited, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(100)]);
        Action dead = () => dying.Task.GetAwaiter().GetResult();
        dead.Should().ThrowExactly<InvalidOperationException>();

        // Negative timeout deterministically crosses the deadline, without sleeping or waiting for wall time.
        var stalled = new TaskCompletionSource<int>();
        wait.Invoke(null, [stalled, alive, TimeSpan.FromTicks(-1), TimeSpan.Zero]);
        Action timeout = () => stalled.Task.GetAwaiter().GetResult();
        timeout.Should().ThrowExactly<TimeoutException>();
        polls.Should().Be(1);

        var duringPoll = new TaskCompletionSource<int>();
        Func<TimeSpan, bool> finish = _ =>
        {
            duringPoll.SetResult(29);
            return false;
        };
        wait.Invoke(null, [duringPoll, finish, RendezvousLimit, TimeSpan.Zero]);
        duringPoll.Task.GetAwaiter().GetResult().Should().Be(29);
    }

    public async Task DispatcherLookupPreservesExplicitPrecedenceAndCachedFailure()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Type attributeType = typeof(UITestMethodAttribute);
        FieldInfo initialized = attributeType.GetField("s_isApplicationInitialized", flags)!;
        FieldInfo cachedQueue = attributeType.GetField("s_applicationDispatcherQueue", flags)!;
        FieldInfo cachedFailure = attributeType.GetField("s_applicationInitializationFailure", flags)!;
        object? previousInitialized = initialized.GetValue(null);
        object? previousQueue = cachedQueue.GetValue(null);
        object? previousFailure = cachedFailure.GetValue(null);
        DispatcherQueue? previousExplicit = UITestMethodAttribute.DispatcherQueue;
        var controller = DispatcherQueueController.CreateOnDedicatedThread();
        MethodInfo getQueue = attributeType.GetMethod("GetDispatcherQueue", flags)!;
        MethodInfo getApplication = attributeType.GetMethod("GetApplicationDispatcherQueue", flags)!;
        Assembly assembly = typeof(WinUIExtensionTests).Assembly;
        try
        {
            UITestMethodAttribute.DispatcherQueue = null;
            cachedQueue.SetValue(null, null);
            cachedFailure.SetValue(null, null);
            initialized.SetValue(null, false);
            getApplication.Invoke(null, [assembly]).Should().BeNull();
            getQueue.Invoke(null, [assembly]).Should().BeNull();
            var method = new Mock<ITestMethod>();
            method.SetupGet(m => m.MethodInfo).Returns(typeof(WinUIExtensionTests).GetMethod(nameof(DispatcherLookupPreservesExplicitPrecedenceAndCachedFailure))!);
            Func<Task> noDispatcher = () => new UITestMethodAttribute().ExecuteAsync(method.Object);
            await noDispatcher.Should().ThrowExactlyAsync<InvalidOperationException>();

            // Hold the real initialization gate and observe the worker blocked on it before publishing
            // the queue. This deterministically exercises the re-check inside the production lock.
            object gate = attributeType.GetField("InitializationLock", flags)!.GetValue(null)!;
            using var started = new ManualResetEventSlim();
            var completed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var worker = new Thread(() =>
            {
                started.Set();
                try
                {
                    completed.TrySetResult(getApplication.Invoke(null, [assembly]));
                }
                catch (Exception exception)
                {
                    completed.TrySetException(exception);
                }
            });
            try
            {
                lock (gate)
                {
                    worker.Start();
                    started.Wait(RendezvousLimit).Should().BeTrue();
                    SpinWait.SpinUntil(() => (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, RendezvousLimit).Should().BeTrue();
                    cachedQueue.SetValue(null, controller.DispatcherQueue);
                }

                (await completed.Task.WaitAsync(RendezvousLimit)).Should().BeSameAs(controller.DispatcherQueue);
            }
            finally
            {
                worker.Join(RendezvousLimit).Should().BeTrue();
            }

            cachedQueue.SetValue(null, null);
            initialized.SetValue(null, true);
            getApplication.Invoke(null, [assembly]).Should().BeNull();
            initialized.SetValue(null, false);
            var failure = new InvalidOperationException("startup root cause");
            cachedFailure.SetValue(null, failure);
            Action firstRead = () => getApplication.Invoke(null, [assembly]);
            Exception first = firstRead.Should().ThrowExactly<TargetInvocationException>().Which.InnerException!;
            first.Should().BeOfType<InvalidOperationException>().Which.InnerException.Should().BeSameAs(failure);
            first.Message.Should().Contain("startup root cause");
            Exception second = firstRead.Should().ThrowExactly<TargetInvocationException>().Which.InnerException!;
            second.Should().NotBeSameAs(first);
            cachedQueue.SetValue(null, controller.DispatcherQueue);
            getApplication.Invoke(null, [assembly]).Should().BeSameAs(controller.DispatcherQueue);
            getQueue.Invoke(null, [assembly]).Should().BeSameAs(controller.DispatcherQueue);
            // Explicit dispatcher takes precedence even over a prior startup failure.
            cachedQueue.SetValue(null, null);
            UITestMethodAttribute.DispatcherQueue = controller.DispatcherQueue;
            getQueue.Invoke(null, [assembly]).Should().BeSameAs(controller.DispatcherQueue);
        }
        finally
        {
            try
            {
                await controller.ShutdownQueueAsync().AsTask().WaitAsync(RendezvousLimit);
            }
            finally
            {
                initialized.SetValue(null, previousInitialized);
                cachedQueue.SetValue(null, previousQueue);
                cachedFailure.SetValue(null, previousFailure);
                UITestMethodAttribute.DispatcherQueue = previousExplicit;
            }
        }
    }

    public async Task NativeApplicationStartupInIsolatedProcesses()
    {
        string? mode = Environment.GetEnvironmentVariable(ChildModeVariable);
        if (mode is not null)
        {
            await CheckNativeStartupAsync(mode);
            return;
        }

        // Each child owns its Application.Start thread. Joining the process also joins its native
        // lifetime, including failure paths that cannot retry Application.Start in the same process.
        foreach (string childMode in new[] { "success", "throw" })
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                start.ArgumentList.Add(typeof(WinUIExtensionTests).Assembly.Location);
            }

            start.ArgumentList.Add("--filter-uid");
            start.ArgumentList.Add(typeof(WinUIExtensionTests).FullName + "." + nameof(NativeApplicationStartupInIsolatedProcesses));
            start.Environment[ChildModeVariable] = childMode;
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(RendezvousLimit);
                }
            }

            string stdout = await output;
            string stderr = await error;
            process.ExitCode.Should().Be(0, $"native startup child {childMode}: {stdout} {stderr}");
            stdout.Should().Contain("succeeded: 1");
            stdout.Should().Contain("skipped: 0");
        }
    }

    private static async Task CheckNativeStartupAsync(string mode)
    {
        Type appType = mode == "success" ? typeof(CoverageApplication) : typeof(ThrowingApplication);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Startup_" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        assembly.SetCustomAttribute(new CustomAttributeBuilder(typeof(WinUITestTargetAttribute).GetConstructor([typeof(Type)])!, [appType]));
        MethodInfo applicationType = typeof(UITestMethodAttribute).GetMethod("GetApplicationType", BindingFlags.Static | BindingFlags.NonPublic)!;
        applicationType.Invoke(null, [assembly]).Should().Be(appType);
        MethodInfo getDispatcher = typeof(UITestMethodAttribute).GetMethod("GetApplicationDispatcherQueue", BindingFlags.Static | BindingFlags.NonPublic)!;
        if (mode == "throw")
        {
            Action startup = () => getDispatcher.Invoke(null, [assembly]);
            startup.Should().ThrowExactly<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>()
                .Which.Message.Should().Be("application constructor failure");
            return;
        }

        var queue = (DispatcherQueue)getDispatcher.Invoke(null, [assembly])!;
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.TryEnqueue(() =>
        {
            Application.Current.Should().BeOfType<CoverageApplication>();
            Application.Current.Exit();
            stopped.TrySetResult(true);
        }).Should().BeTrue();
        (await stopped.Task.WaitAsync(RendezvousLimit)).Should().BeTrue();
    }

    public async Task RealDispatcherCapturesDirectAndEnqueuedResultsAndFailures()
    {
        DispatcherQueue? previous = UITestMethodAttribute.DispatcherQueue;
        var controller = DispatcherQueueController.CreateOnDedicatedThread();
        bool shutdown = false;
        try
        {
            UITestMethodAttribute.DispatcherQueue = controller.DispatcherQueue;
            var attribute = new UITestMethodAttribute("contracts.cs", 17);
            var result = new TestResult { Outcome = UnitTestOutcome.Passed, DisplayName = "dispatched" };
            var method = new Mock<ITestMethod>();
            method.SetupGet(m => m.MethodInfo).Returns(typeof(WinUIExtensionTests).GetMethod(nameof(RealDispatcherCapturesDirectAndEnqueuedResultsAndFailures))!);
            method.Setup(m => m.InvokeAsync(null)).Returns(() =>
            {
                controller.DispatcherQueue.HasThreadAccess.Should().BeTrue();
                return Task.FromResult(result);
            });
            TestResult[] enqueued = await attribute.ExecuteAsync(method.Object).WaitAsync(RendezvousLimit);
            enqueued.Should().ContainSingle().Which.Should().BeSameAs(result);

            var failure = new InvalidOperationException("dispatcher failure");
            method.Setup(m => m.InvokeAsync(null)).Throws(failure);
            // Production completes its TCS inline on the queue, so a prior await can resume this test
            // on that queue. Explicitly dispatch from a pool worker to prove the enqueued catch path.
            (await Task.Run(() => attribute.ExecuteAsync(method.Object)).WaitAsync(RendezvousLimit)).Single().TestFailureException.Should().BeSameAs(failure);

            var direct = new TaskCompletionSource<TestResult[]>(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable VSTHRD101 // Callback is joined through the completion signal before queue shutdown.
            controller.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    direct.SetResult(await attribute.ExecuteAsync(method.Object));
                }
                catch (Exception exception)
                {
                    direct.SetException(exception);
                }
            }).Should().BeTrue();
#pragma warning restore VSTHRD101
            (await direct.Task.WaitAsync(RendezvousLimit)).Single().TestFailureException.Should().BeSameAs(failure);

            method.Setup(m => m.InvokeAsync(null)).ReturnsAsync(result);
            var directSuccess = new TaskCompletionSource<TestResult[]>(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable VSTHRD101 // Callback is joined through the completion signal before queue shutdown.
            controller.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    directSuccess.SetResult(await attribute.ExecuteAsync(method.Object));
                }
                catch (Exception exception)
                {
                    directSuccess.SetException(exception);
                }
            }).Should().BeTrue();
#pragma warning restore VSTHRD101
            (await directSuccess.Task.WaitAsync(RendezvousLimit)).Single().Should().BeSameAs(result);

            await controller.ShutdownQueueAsync().AsTask().WaitAsync(RendezvousLimit);
            shutdown = true;
            (await attribute.ExecuteAsync(method.Object).WaitAsync(RendezvousLimit)).Should().ContainSingle().Which.Should().BeNull();
        }
        finally
        {
            try
            {
                if (!shutdown)
                {
                    await controller.ShutdownQueueAsync().AsTask().WaitAsync(RendezvousLimit);
                }
            }
            finally
            {
                UITestMethodAttribute.DispatcherQueue = previous;
            }
        }
    }

    public sealed class CoverageApplication : Application
    {
    }

    public sealed class ThrowingApplication : Application
    {
        public ThrowingApplication() => throw new InvalidOperationException("application constructor failure");
    }
}
#endif
