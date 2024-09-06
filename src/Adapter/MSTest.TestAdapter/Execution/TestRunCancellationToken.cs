// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Cancellation token supporting cancellation of a test run.
/// </summary>
public class TestRunCancellationToken
{
    /// <summary>
    /// Callbacks to be invoked when canceled.
    /// </summary>
    private readonly List<Action> _registeredCallbacks = new();

    /// <summary>
    /// Stores whether the test run is canceled or not.
    /// </summary>
    private bool _canceled;

    public TestRunCancellationToken()
        : this(CancellationToken.None)
    {
    }

    internal TestRunCancellationToken(CancellationToken cancellationToken) => CancellationToken = cancellationToken;

    internal CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets a value indicating whether the test run is canceled.
    /// </summary>
    public bool Canceled
    {
        get => _canceled;

        private set
        {
            bool previousValue = _canceled;
            _canceled = value;

            if (!previousValue && value)
            {
                foreach (Action callBack in _registeredCallbacks)
                {
                    callBack.Invoke();
                }
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
    public void Register(Action callback) => _registeredCallbacks.Add(callback);

    /// <summary>
    /// Unregister the callback method.
    /// </summary>
    public void Unregister() => _registeredCallbacks.Clear();

    internal void ThrowIfCancellationRequested()
    {
        CancellationToken.ThrowIfCancellationRequested();

        if (Canceled)
        {
            if (CancellationToken == CancellationToken.None)
            {
                throw new OperationCanceledException();
            }
            else
            {
                throw new OperationCanceledException(CancellationToken);
            }
        }
    }
}
