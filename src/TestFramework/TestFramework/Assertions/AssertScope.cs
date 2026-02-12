// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents a scope in which assertion failures are collected instead of thrown immediately.
/// When the scope is disposed, all collected failures are thrown as a single <see cref="AssertFailedException"/>.
/// </summary>
internal sealed class AssertScope : IDisposable
{
    private static readonly AsyncLocal<AssertScope?> CurrentScope = new();

    private readonly ConcurrentQueue<AssertFailedException> _errors = new();
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
    /// Adds an assertion failure message to the current scope.
    /// </summary>
    /// <param name="error">The assertion failure message.</param>
    internal void AddError(AssertFailedException error)
    {
#pragma warning disable CA1513 // Use ObjectDisposedException throw helper
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AssertScope));
        }
#pragma warning restore CA1513 // Use ObjectDisposedException throw helper

        _errors.Enqueue(error);
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

        if (_errors.Count == 1 && _errors.TryDequeue(out AssertFailedException? singleError))
        {
            Assert.LaunchDebuggerIfNeeded();
            throw singleError;
        }

        if (!_errors.IsEmpty)
        {
            Assert.LaunchDebuggerIfNeeded();
            throw new AssertFailedException(
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertScopeFailure, _errors.Count),
                new AggregateException(_errors));
        }
    }
}
