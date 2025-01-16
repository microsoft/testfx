// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides the information needed for executing assembly initialize.
/// This type is passed as a parameter to <see cref="AssemblyInitializeAttribute.ExecuteAsync(AssemblyInitializeExecutionContext)"/>.
/// </summary>
public readonly struct AssemblyInitializeExecutionContext
{
    internal AssemblyInitializeExecutionContext(Func<Task> assemblyInitializeExecutorGetter)
        => AssemblyInitializeExecutorGetter = assemblyInitializeExecutorGetter;

    /// <summary>
    /// Gets the <see cref="Func{Task}"/> that returns the <see cref="Task"/> that executes the AssemblyInitialize method.
    /// </summary>
    public Func<Task> AssemblyInitializeExecutorGetter { get; }
}
