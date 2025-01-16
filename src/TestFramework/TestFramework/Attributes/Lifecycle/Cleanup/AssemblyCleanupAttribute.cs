// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The assembly cleanup attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AssemblyCleanupAttribute : Attribute
{
    /// <summary>
    /// Executes the assembly cleanup method. Custom <see cref="AssemblyCleanupAttribute"/> implementations may
    /// override this method to plug in custom logic for executing assembly cleanup.
    /// </summary>
    /// <param name="assemblyCleanupContext">A struct to hold information for executing the assembly cleanup.</param>
    public virtual async Task ExecuteAsync(AssemblyCleanupExecutionContext assemblyCleanupContext)
        => await assemblyCleanupContext.AssemblyCleanupExecutorGetter();
}
