// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The assembly initialize attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class AssemblyInitializeAttribute : Attribute
{
    /// <summary>
    /// Executes the assembly initialize method. Custom <see cref="AssemblyInitializeAttribute"/> implementations may
    /// override this method to plug in custom logic for executing assembly initialize.
    /// </summary>
    /// <param name="assemblyInitializeContext">A struct to hold information for executing the assembly initialize.</param>
    public virtual async Task ExecuteAsync(AssemblyInitializeExecutionContext assemblyInitializeContext)
        => await assemblyInitializeContext.AssemblyInitializeExecutorGetter();
}
