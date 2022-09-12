// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TestFramework.ForTestingMSTest;

/// <summary>
/// Inherit from this class to be recognized as a test class.
/// All public parameterless methods will be recognized as test methods.
/// Use constructor as a "before each test" initialization and override Dispose(bool) for "after each test" behavior.
/// </summary>
public abstract class TestContainer : IDisposable
{
    protected bool IsDisposed { get; private set; }

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

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Not static to avoid CA1822 warning on test methods")]
    protected void Verify(bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = default,
        [CallerMemberName] string? caller = default,
        [CallerFilePath] string? filePath = default,
        [CallerLineNumber] int lineNumber = default)
    {
        if (!condition)
            Throw(expression, caller, filePath, lineNumber);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Not static to avoid CA1822 warning on test methods")]
    protected Exception VerifyThrows(Action action,
        [CallerArgumentExpression(nameof(action))] string? expression = default,
        [CallerMemberName] string? caller = default,
        [CallerFilePath] string? filePath = default,
        [CallerLineNumber] int lineNumber = default)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            return ex;
        }

        Throw(expression, caller, filePath, lineNumber);
        return null;
    }

    [DoesNotReturn]
    private static void Throw(string? expression, string? caller, string? filePath, int lineNumber)
        => throw new Exception($"Expression failed: {expression ?? "<expression>"} at line {lineNumber} of method {caller ?? "<caller>"} in file {filePath ?? "<file-path>"}.");
}
