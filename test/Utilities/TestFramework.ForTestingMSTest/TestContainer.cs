// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TestFramework.ForTestingMSTest;

/// <summary>
/// Inherit from this class to be recognized as a test class.
/// All public parameterless methods will be recognized as test methods.
/// Use constructor as a "before each test" initialization and override Dispose(bool) for "after each test" behavior.
/// </summary>
public abstract class TestContainer : IDisposable
{
    internal static readonly string IsVerifyException = nameof(IsVerifyException);

    /// <summary>
    /// Initializes a new instance of the <see cref="TestContainer"/> class.
    /// Constructor is used to provide some initialization before each test.
    /// </summary>
    protected TestContainer()
    {
    }

    protected bool IsDisposed { get; private set; }

    /// <summary>
    /// Override this method to provide some cleanup after each test.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null
            IsDisposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
