// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer
{
    using global::System;
    using global::System.Runtime.CompilerServices;

    /// <summary>
    /// Execute test code in UI thread for Windows store apps.
    /// </summary>
    public class UITestMethodAttribute : TestMethodAttribute
    {
        /// <summary>
        /// Gets or sets the <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> that should be used to invoke the UITestMethodAttribute.
        /// If none is provided, it will try to use the Microsoft.UI.Xaml.Window.Current.DispatcherQueue, which only works on UWP.
        /// </summary>
        public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; set; }

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
            var attrib = testMethod.GetAttributes<AsyncStateMachineAttribute>(false);
            if (attrib.Length > 0)
            {
                throw new NotSupportedException(FrameworkMessages.AsyncUITestMethodNotSupported);
            }

            TestResult result = null;

            var dispatcher = DispatcherQueue ?? global::Microsoft.UI.Xaml.Window.Current?.DispatcherQueue;
            if (dispatcher == null)
            {
                throw new InvalidOperationException(FrameworkMessages.AsyncUITestMethodWithNoDispatcherQueue);
            }

            if (dispatcher.HasThreadAccess)
            {
                try
                {
                    result = testMethod.Invoke(null);
                }
                catch (Exception e)
                {
                    return new TestResult[] { new TestResult { TestFailureException = e } };
                }
            }
            else
            {
                var taskCompletionSource = new global::System.Threading.Tasks.TaskCompletionSource<object>();

                if (!dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
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

            return new TestResult[] { result };
        }
    }
}
