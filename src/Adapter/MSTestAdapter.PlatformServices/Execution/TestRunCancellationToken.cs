// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Cancellation token supporting cancellation of a test run.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
internal sealed class TestRunCancellationToken
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly CancellationToken? _originalCancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestRunCancellationToken"/> class.
    /// </summary>
    public TestRunCancellationToken()
    {
    }

    internal TestRunCancellationToken(CancellationToken originalCancellationToken)
        => _originalCancellationToken = originalCancellationToken;

    /// <summary>
    /// Gets a value indicating whether the test run is canceled.
    /// </summary>
    public bool Canceled
    {
        get;
        private set
        {
            bool previousValue = field;
            field = value;

            if (!previousValue && value)
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }

    /// <summary>
    /// Cancels the execution of a test run.
    /// </summary>
    public void Cancel() => Canceled = true;

    /// <summary>
    /// Registers a callback method to be invoked when canceled.
    /// </summary>
    /// <param name="callback">Callback delegate for handling cancellation.</param>
    public void Register(Action callback) => _cancellationTokenSource.Token.Register(_ => callback(), null);

    internal CancellationTokenRegistration Register(Action<object?> callback, object? state) => _cancellationTokenSource.Token.Register(callback, state);

    /// <summary>
    /// This method does nothing and is kept for binary compatibility. It should be removed in v4.
    /// Note that the whole class is marked obsolete and will be made internal in v4.
    /// </summary>
    public void Unregister()
    {
    }

    internal void ThrowIfCancellationRequested()
        // If ThrowIfCancellationRequested is called from the main AppDomain where we have the original
        // cancellation token, we should use that token.
        // Otherwise, we have no choice other than using the cancellation token from the source created by TestRunCancellationToken.
        => (_originalCancellationToken ?? _cancellationTokenSource.Token).ThrowIfCancellationRequested();
}
