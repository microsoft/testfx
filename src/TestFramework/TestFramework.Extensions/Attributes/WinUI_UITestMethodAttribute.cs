// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WIN_UI
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

/// <summary>
/// Execute test code in UI thread for Windows store apps.
/// </summary>
public class UITestMethodAttribute : TestMethodAttribute
{
    private static bool s_isApplicationInitialized;
    private static DispatcherQueue? s_applicationDispatcherQueue;

    /// <summary>
    /// Initializes a new instance of the <see cref="UITestMethodAttribute"/> class.
    /// </summary>
    public UITestMethodAttribute()
        : base()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UITestMethodAttribute"/> class.
    /// </summary>
    /// <param name="displayName">
    /// Display Name for the test.
    /// </param>
    public UITestMethodAttribute(string displayName)
        : base(displayName)
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
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        AsyncStateMachineAttribute[] attribute = testMethod.GetAttributes<AsyncStateMachineAttribute>(false);
        if (attribute.Length > 0)
        {
            throw new NotSupportedException(FrameworkMessages.AsyncUITestMethodNotSupported);
        }

        TestResult? result = null;

        // TODO: Code seems to be assuming DeclaringType is never null, but it can be null.
        // Using 'bang' notation for now to ensure same behavior.
        DispatcherQueue dispatcher = GetDispatcherQueue(testMethod.MethodInfo.DeclaringType!.Assembly) ?? throw new InvalidOperationException(FrameworkMessages.AsyncUITestMethodWithNoDispatcherQueue);
        if (dispatcher.HasThreadAccess)
        {
            try
            {
                result = testMethod.Invoke(null);
            }
            catch (Exception e)
            {
                return [new() { TestFailureException = e }];
            }
        }
        else
        {
            var taskCompletionSource = new TaskCompletionSource<object?>();

            if (!dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                {
                    try
                    {
                        result = testMethod.Invoke(Array.Empty<object>());
                        taskCompletionSource.SetResult(null);
                    }
                    catch (Exception e)
                    {
                        result = new TestResult { TestFailureException = e };
                        taskCompletionSource.SetException(e);
                    }
                }))
            {
                taskCompletionSource.SetResult(null);
            }

            taskCompletionSource.Task.GetAwaiter().GetResult();
        }

        return [result!];
    }

    private static Type? GetApplicationType(Assembly assembly)
    {
        WinUITestTargetAttribute? attribute = assembly.GetCustomAttribute<WinUITestTargetAttribute>();
        return attribute == null || attribute.ApplicationType == null ? null : attribute.ApplicationType;
    }

    private static DispatcherQueue? GetApplicationDispatcherQueue(Assembly assembly)
    {
        if (s_applicationDispatcherQueue != null)
        {
            return s_applicationDispatcherQueue;
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
                tsc.SetResult(dispatcher);
            }
            catch
            {
            }
        }

        var treadStart = new ThreadStart(() => Application.Start(OnApplicationInitialized));
        var uiThread = new Thread(treadStart)
        {
            Name = "UI Thread for Tests",
        };
        uiThread.Start();
        tsc.Task.Wait();

        return tsc.Task.Result;
    }
}
#endif
