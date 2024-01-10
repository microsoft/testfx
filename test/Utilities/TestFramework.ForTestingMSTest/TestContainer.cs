// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    internal static readonly string IsVerifyException = nameof(IsVerifyException);

    /// <summary>
    /// Initializes a new instance of the <see cref="TestContainer"/> class.
    /// Constructor is used to provide some initialization before each test.
    /// </summary>
    public TestContainer()
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

    public static void Verify(
        bool condition,
        [CallerArgumentExpression(nameof(condition))] string? expression = default,
        [CallerMemberName] string? caller = default,
        [CallerFilePath] string? filePath = default,
        [CallerLineNumber] int lineNumber = default)
    {
        if (!condition)
        {
            Throw(expression, caller, filePath, lineNumber);
        }
    }

    public static Exception VerifyThrows(
        Action action,
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

    public static void Fail(
        [CallerMemberName] string? caller = default,
        [CallerFilePath] string? filePath = default,
        [CallerLineNumber] int lineNumber = default)
    {
        Throw(string.Empty, caller, filePath, lineNumber);
    }

    [DoesNotReturn]
    private static void Throw(string? expression, string? caller, string? filePath, int lineNumber)
    {
        var verifyException = new Exception($"Verification failed for {expression ?? "<expression>"} at line {lineNumber} of method '{caller ?? "<caller>"}' in file '{filePath ?? "<file-path>"}'.");
        verifyException.Data.Add(IsVerifyException, true);
        throw verifyException;
    }
}
