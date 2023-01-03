// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WINDOWS_UWP
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

/// <summary>
/// Execute test code in UI thread for Windows store apps.
/// </summary>
public class UITestMethodAttribute : TestMethodAttribute
{
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
    public override async Task<TestResult[]> Execute(ITestMethod testMethod)
    {
        var attribute = testMethod.GetAttributes<AsyncStateMachineAttribute>(false);
        if (attribute.Length > 0)
        {
            throw new NotSupportedException(FrameworkMessages.AsyncUITestMethodNotSupported);
        }

        TestResult? result = null;
        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal,
            async () =>
            {
                result = await testMethod.Invoke(null);
            });

        return new TestResult[] { result! };
    }
}
#endif
