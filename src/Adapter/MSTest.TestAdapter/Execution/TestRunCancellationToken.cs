// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Cancellation token supporting cancellation of a test run.
/// </summary>
public class TestRunCancellationToken
{
    /// <summary>
    /// Stores whether the test run is canceled or not.
    /// </summary>
    private bool _canceled;

    /// <summary>
    /// Callback to be invoked when canceled.
    /// </summary>
    private Action? _registeredCallback;

    public TestRunCancellationToken()
        : this(CancellationToken.None)
    {
    }

    internal TestRunCancellationToken(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    internal CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets a value indicating whether the test run is canceled.
    /// </summary>
    public bool Canceled
    {
        get => _canceled;

        private set
        {
            _canceled = value;
            if (_canceled)
            {
                _registeredCallback?.Invoke();
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
    public void Register(Action callback)
    {
        _ = callback ?? throw new ArgumentNullException(nameof(callback));

        DebugEx.Assert(_registeredCallback == null, "Callback delegate is already registered, use a new cancellationToken");

        _registeredCallback = callback;
    }

    /// <summary>
    /// Unregister the callback method.
    /// </summary>
    public void Unregister() => _registeredCallback = null;
}
