// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WIN_UI
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

/// <summary>
/// Execute test code in UI thread for Windows store apps.
/// </summary>
public class UITestMethodAttribute : TestMethodAttribute
{
    // How often the application-startup wait re-checks that the UI thread is still alive. Short enough
    // that a failed startup is reported promptly, long enough not to spin.
    private static readonly TimeSpan ThreadLivenessPollInterval = TimeSpan.FromMilliseconds(100);

    // Upper bound on how long to wait for the application to come up. Application.Start can block without
    // ever invoking its callback (for example a modal dialog during XAML initialization), which neither an
    // exception nor the thread dying would surface, so the wait is capped to keep a startup problem a test
    // failure rather than a hung run. Generous because it covers first-run Windows App SDK resolution.
    private static readonly TimeSpan ApplicationStartupTimeout = TimeSpan.FromMinutes(2);

    // Serializes the check-and-start below. Application.Start can only be called once per process, so
    // without this two [UITestMethod]s running in parallel could both observe "not started yet" and each
    // start an application.
    private static readonly object InitializationLock = new();

    // Written on the UI thread and read on the test thread, so all three are volatile: this runs on ARM64
    // as well as x64, where non-volatile reads are not guaranteed to observe the writes.
    private static volatile bool s_isApplicationInitialized;
    private static volatile DispatcherQueue? s_applicationDispatcherQueue;

    // Why bringing the application up failed, so that every later [UITestMethod] reports the same root
    // cause instead of the generic "DispatcherQueue should not be null" message. The cause is stored
    // rather than an ExceptionDispatchInfo because replaying one dispatch token from several test threads
    // would mutate the stack trace of a single shared exception; each caller instead gets a fresh
    // exception wrapping this one. Application.Start cannot meaningfully be retried in a process, so
    // re-reporting is better than re-attempting.
    private static volatile Exception? s_applicationInitializationFailure;

    /// <summary>
    /// Initializes a new instance of the <see cref="UITestMethodAttribute"/> class.
    /// </summary>
    public UITestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    /// <summary>
    /// Gets or sets the <see cref="UI.Dispatching.DispatcherQueue"/> that should be used to invoke the UITestMethodAttribute.
    /// If none is provided <see cref="UITestMethodAttribute"/> will check for <see cref="WinUITestTargetAttribute" />, if the attribute is defined it will start the App and use its <see cref="UI.Dispatching.DispatcherQueue"/>.
    /// <see cref="UITestMethodAttribute"/> will try to use <c>Microsoft.UI.Xaml.Window.Current.DispatcherQueue</c> for the last resort, but that will only work on UWP.
    /// </summary>
    public static DispatcherQueue? DispatcherQueue { get; set; }

    /// <summary>
    /// Executes the test method on the UI Thread.
    /// </summary>
    /// <param name="testMethod">
    /// The test method.
    /// </param>
    /// <returns>
    /// An array of <see cref="TestResult"/> instances.
    /// </returns>
    /// Throws <exception cref="NotSupportedException"> when run on an async test method.
    /// </exception>
    public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        // This code assumes DeclaringType is never null, but it can be null.
        // Using 'bang' notation for now to ensure same behavior.
        DispatcherQueue dispatcher = GetDispatcherQueue(testMethod.MethodInfo.DeclaringType!.Assembly) ?? throw new InvalidOperationException(FrameworkExtensionsMessages.AsyncUITestMethodWithNoDispatcherQueue);
        if (dispatcher.HasThreadAccess)
        {
            try
            {
                return [await testMethod.InvokeAsync(null).ConfigureAwait(false)];
            }
            catch (Exception e)
            {
                return [new() { TestFailureException = e }];
            }
        }

        var tcs = new TaskCompletionSource<TestResult>();

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        if (!dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
        {
            try
            {
                tcs.SetResult(await testMethod.InvokeAsync(null).ConfigureAwait(false));
            }
            catch (Exception e)
            {
                tcs.SetResult(new TestResult { TestFailureException = e });
            }
        }))
        {
            tcs.SetResult(null!);
        }
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

        return [await tcs.Task.ConfigureAwait(false)];
    }

    private static Type? GetApplicationType(Assembly assembly)
        => assembly.GetCustomAttribute<WinUITestTargetAttribute>()?.ApplicationType;

    private static DispatcherQueue? GetApplicationDispatcherQueue(Assembly assembly)
    {
        if (s_applicationDispatcherQueue != null)
        {
            return s_applicationDispatcherQueue;
        }

        // Application.Start can only be called once per process, so the whole check-and-start has to be
        // serialized: otherwise two [UITestMethod]s running in parallel can both see "not started yet".
        lock (InitializationLock)
        {
            if (s_applicationDispatcherQueue != null)
            {
                return s_applicationDispatcherQueue;
            }

            // A previous attempt already failed to bring the application up, and Application.Start cannot
            // be retried in the same process. Report that cause instead of letting the caller fall through
            // to the generic "DispatcherQueue should not be null" message. A new exception is built per
            // caller so concurrent tests never share (and mutate) one exception instance.
            if (s_applicationInitializationFailure is { } cause)
            {
                // The cause's message is formatted into the text rather than relying on the inner
                // exception alone: MSTest reports a failure using the exception's Message, so an
                // InnerException would never be shown to the user. It is still attached for debuggers
                // and for anyone inspecting the exception programmatically.
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture, FrameworkExtensionsMessages.WinUIApplicationInitializationAlreadyFailed, cause.Message),
                    cause);
            }

            if (s_isApplicationInitialized)
            {
                return null;
            }

            Type? applicationType = GetApplicationType(assembly);
            if (applicationType == null)
            {
                return null;
            }

            // We need to initialize the SDK before calling Application.Start
            try
            {
                // We need to execute all module initializers before doing any WinRT calls.
                // This will cause the [ModuleInitializer]s to execute, if they haven't yet.
                Type? id = applicationType.Assembly.GetType("Microsoft.WindowsAppSDK.Runtime.Identity");
                if (id != null)
                {
                    _ = Activator.CreateInstance(id);
                }
            }
            catch
            {
            }

            return InitializeApplication(applicationType);
        }
    }

    private static DispatcherQueue? GetDispatcherQueue(Assembly assembly)
    {
        if (DispatcherQueue != null)
        {
            return DispatcherQueue;
        }

        if (GetApplicationDispatcherQueue(assembly) is { } appDispatcherQueue)
        {
            return appDispatcherQueue;
        }

        try
        {
            if (Window.Current?.DispatcherQueue is { } windowDispatcherQueue)
            {
                return windowDispatcherQueue;
            }
        }
        catch
        {
        }

        return null;
    }

    private static DispatcherQueue InitializeApplication(Type applicationType)
    {
        var tsc = new TaskCompletionSource<DispatcherQueue>();
        void OnApplicationInitialized(ApplicationInitializationCallbackParams e)
        {
            try
            {
                s_isApplicationInitialized = true;
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                var context = new DispatcherQueueSynchronizationContext(dispatcher);
                SynchronizationContext.SetSynchronizationContext(context);

                _ = Activator.CreateInstance(applicationType) as Application;
                s_applicationDispatcherQueue = dispatcher;
                tsc.TrySetResult(dispatcher);
            }
            catch (Exception ex)
            {
                // Never leave the wait below blocked. This used to swallow the exception, which left the
                // TaskCompletionSource incomplete and hung the whole run on tsc.Task with no diagnostic at
                // all. The common causes are a throwing Application constructor and — in an unpackaged app
                // — the Windows App SDK runtime not being resolvable, which surfaces here as a COMException
                // (REGDB_E_CLASSNOTREG). Propagate it so the test fails with the real cause. See issue #2784.
                //
                // Unwrap the reflection wrapper: MSTest reports a failure using the exception's Message, so
                // leaving a TargetInvocationException in place would tell the user only "Exception has been
                // thrown by the target of an invocation" and bury the real cause in the stack trace.
                tsc.TrySetException(ex is TargetInvocationException { InnerException: { } inner } ? inner : ex);
            }
        }

        var threadStart = new ThreadStart(() =>
        {
            try
            {
                Application.Start(OnApplicationInitialized);

                // Application.Start normally pumps a message loop until the app exits, so reaching this
                // point without the callback having completed the wait means the application never came up.
                tsc.TrySetException(new InvalidOperationException(FrameworkExtensionsMessages.WinUIApplicationFailedToStart));
            }
            catch (Exception ex)
            {
                // Application.Start can throw before it ever invokes the callback above (typically when the
                // Windows App SDK runtime cannot be resolved). Without this the exception would be unhandled
                // on this thread and the wait below would never be released.
                tsc.TrySetException(ex);
            }
        });
        var uiThread = new Thread(threadStart)
        {
            Name = "UI Thread for Tests",

            // Application.Start pumps a message loop that never returns in the WinUITestTarget
            // model, so a foreground thread would keep the process alive after the run and hang when
            // the test app is itself the process (unpackaged WinUI under Microsoft.Testing.Platform).
            // A background thread lets the process exit once the run completes. See issue #2784.
            IsBackground = true,
        };
        uiThread.Start();

        // Wait for the application to come up, but never unconditionally. Two things can leave the wait
        // incomplete and used to hang the whole run with no diagnostic at all (see issue #2784):
        //  - XAML tears this thread down for some native startup failures without surfacing a managed
        //    exception, so neither catch above runs. A dead thread therefore counts as a failure.
        //  - Startup can stall while the thread stays alive: Application.Start may block without ever
        //    invoking its callback (for example a modal dialog during XAML initialization), or the
        //    callback may block constructing the application. The overall timeout covers both.
        // On success the callback completes the wait while the thread keeps pumping, so this costs at
        // most one poll interval.
        WaitForApplicationStartup(tsc, uiThread.Join, ApplicationStartupTimeout, ThreadLivenessPollInterval);

        try
        {
            // GetAwaiter().GetResult() rather than Wait()/Result so a failure surfaces as the original
            // exception instead of an AggregateException wrapping it.
            return tsc.Task.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Remember why startup failed so later [UITestMethod]s report the same cause instead of a
            // generic message. A bare 'throw' preserves the original stack.
            s_applicationInitializationFailure = ex;
            throw;
        }
    }

    /// <summary>
    /// Waits for application startup to complete, and fails the wait rather than blocking forever when
    /// the UI thread dies or startup stalls.
    /// </summary>
    /// <remarks>
    /// Separated from the thread it observes so that both failure paths can be covered by tests without a
    /// real XAML application: they are the guards that keep a startup problem a test failure rather than
    /// the hung run described in issue #2784, and neither is reproducible on demand from a test host.
    /// </remarks>
    /// <typeparam name="T">The result the startup completion carries.</typeparam>
    /// <param name="startupCompletion">Completed by the initialization callback once the application is up.</param>
    /// <param name="waitForUiThreadExit">Waits the given interval for the UI thread to exit, returning whether it did.</param>
    /// <param name="timeout">How long startup may take before it is reported as stalled.</param>
    /// <param name="pollInterval">How long each liveness check waits for the UI thread to exit.</param>
    internal static void WaitForApplicationStartup<T>(
        TaskCompletionSource<T> startupCompletion,
        Func<TimeSpan, bool> waitForUiThreadExit,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var startupTimer = Stopwatch.StartNew();
        while (!startupCompletion.Task.IsCompleted)
        {
            if (waitForUiThreadExit(pollInterval))
            {
                startupCompletion.TrySetException(new InvalidOperationException(FrameworkExtensionsMessages.WinUIApplicationFailedToStart));
                return;
            }

            if (startupTimer.Elapsed > timeout)
            {
                // The thread is still alive, so the application did not exit — it never finished coming
                // up. Say that rather than reusing the "exited without initializing" message, which would
                // point at the wrong cause.
                startupCompletion.TrySetException(new TimeoutException(
                    string.Format(CultureInfo.CurrentCulture, FrameworkExtensionsMessages.WinUIApplicationStartupTimedOut, timeout)));
                return;
            }
        }
    }
}
#endif
