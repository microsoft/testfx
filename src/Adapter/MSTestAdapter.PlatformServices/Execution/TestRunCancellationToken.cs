// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Cancellation token supporting cancellation of a test run.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(TestTools.UnitTesting.FrameworkConstants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(TestTools.UnitTesting.FrameworkConstants.PublicTypeObsoleteMessage)]
#endif
public class TestRunCancellationToken
{
    /// <summary>
    /// Callbacks to be invoked when canceled.
    /// Needs to be a concurrent collection, see https://github.com/microsoft/testfx/issues/3953.
    /// </summary>
    private readonly ConcurrentBag<(Action<object?>, object?)> _registeredCallbacks = [];

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
                foreach ((Action<object?> callBack, object? state) in _registeredCallbacks)
                {
                    callBack.Invoke(state);
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
    public void Register(Action callback) => _registeredCallbacks.Add((_ => callback(), null));

    internal void Register(Action<object?> callback, object? state) => _registeredCallbacks.Add((callback, state));

    /// <summary>
    /// Unregister the callback method.
    /// </summary>
    public void Unregister()
#if NETCOREAPP || WINDOWS_UWP
        => _registeredCallbacks.Clear();
#else
    {
        while (!_registeredCallbacks.IsEmpty)
        {
            _ = _registeredCallbacks.TryTake(out _);
        }
    }
#endif

    internal void ThrowIfCancellationRequested()
    {
        if (Canceled)
        {
            throw new OperationCanceledException();
        }
    }
}
