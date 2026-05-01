// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents a scope in which assertion failures are collected instead of thrown immediately.
/// When the scope is disposed, all collected failures are thrown as a single <see cref="AssertFailedException"/>.
/// </summary>
internal sealed class AssertScope : IDisposable
{
    private static readonly AsyncLocal<AssertScope?> CurrentScope = new();

    private readonly ConcurrentQueue<ExceptionDispatchInfo> _errors = new();
    private bool _disposed;

    internal AssertScope()
    {
        if (CurrentScope.Value is not null)
        {
            throw new InvalidOperationException(FrameworkMessages.AssertScopeNestedNotAllowed);
        }

        CurrentScope.Value = this;
    }

    /// <summary>
    /// Gets the current active <see cref="AssertScope"/>, or <see langword="null"/> if no scope is active.
    /// </summary>
    internal static AssertScope? Current => CurrentScope.Value;

    /// <summary>
    /// Adds an assertion failure to the current scope, capturing the exception dispatch info
    /// to preserve the original stack trace from the assertion call site.
    /// </summary>
    /// <param name="error">The assertion failure exception.</param>
    internal void AddError(AssertFailedException error)
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
#pragma warning disable CA1513
        if (_disposed)
            throw new ObjectDisposedException(nameof(AssertScope));
#pragma warning restore CA1513
#endif

        _errors.Enqueue(ExceptionDispatchInfo.Capture(error));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CurrentScope.Value = null;

        // We throw the collected exceptions directly instead of going through assertion failure
        // helpers (e.g. ThrowAssertFailed) because the debugger was already launched when each
        // error was collected.
        // Snapshot the ConcurrentQueue into an array to avoid multiple O(n) enumerations
        // (ConcurrentQueue<T>.Count is O(n)) and to get a consistent view for branching,
        // message formatting, and building the AggregateException.
        ExceptionDispatchInfo[] errorsSnapshot = _errors.ToArray();
        if (errorsSnapshot.Length == 1)
        {
            // Re-throw preserving the original stack trace captured at the assertion call site.
            errorsSnapshot[0].Throw();
        }

        if (errorsSnapshot.Length > 0)
        {
            throw new AssertFailedException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertScopeFailure, errorsSnapshot.Length),
                new AggregateException(Array.ConvertAll(errorsSnapshot, static edi => edi.SourceException)));
        }
    }
}
