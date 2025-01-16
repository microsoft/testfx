// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides the information needed for executing assembly cleanup.
/// This type is passed as a parameter to <see cref="AssemblyCleanupAttribute.ExecuteAsync(AssemblyCleanupExecutionContext)"/>.
/// </summary>
public readonly struct AssemblyCleanupExecutionContext
{
    internal AssemblyCleanupExecutionContext(Func<Task> assemblyCleanupExecutorGetter)
        => AssemblyCleanupExecutorGetter = assemblyCleanupExecutorGetter;

    /// <summary>
    /// Gets the <see cref="Func{Task}"/> that returns the <see cref="Task"/> that executes the AssemblyCleanup method.
    /// </summary>
    public Func<Task> AssemblyCleanupExecutorGetter { get; }
}
