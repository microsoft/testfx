﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MSTest.ForTesting.TestAdapter;

/// <summary>
/// Inherit from this class to be recognized as a test class.
/// All public parameterless methods will be recognized as test methods.
/// Use constructor as a "before each test" initialization and override Dispose(bool) for "after each test" behavior.
/// </summary>
public abstract class TestContainer : IDisposable
{
    private bool _isDisposed;

    /// <summary>
    /// Constructor is used to provide some initialization before each test.
    /// </summary>
    public TestContainer()
    {

    }

    /// <summary>
    /// Override this method to provide some cleanup after each test.
    /// </summary>
    /// <param name="disposing"></param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null
            _isDisposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
