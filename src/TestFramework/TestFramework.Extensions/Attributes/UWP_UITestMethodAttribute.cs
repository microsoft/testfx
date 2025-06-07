// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if WINDOWS_UWP

namespace Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

/// <summary>
/// Execute test code in UI thread for Windows store apps.
/// </summary>
public class UITestMethodAttribute : TestMethodAttribute
{
    private protected override bool UseAsync => GetType() == typeof(UITestMethodAttribute);

    /// <inheritdoc cref="ExecuteAsync(ITestMethod)" />
    public override TestResult[] Execute(ITestMethod testMethod) => base.Execute(testMethod);

    /// <summary>
    /// Executes the test method on the UI Thread.
    /// </summary>
    /// <param name="testMethod">
    /// The test method.
    /// </param>
    /// <returns>
    /// An array of <see cref="TestResult"/> instances.
    /// </returns>
    internal override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
    {
        var tcs = new TaskCompletionSource<TestResult>();
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal,
            async () =>
            {
                try
                {
                    tcs.SetResult(await testMethod.InvokeAsync(null));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

        return [await tcs.Task];
    }
}
#endif
