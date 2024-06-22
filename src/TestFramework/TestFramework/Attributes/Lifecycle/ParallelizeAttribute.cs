// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specification for parallelization level for a test run.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public class ParallelizeAttribute : Attribute
{
    private const int DefaultParallelWorkers = 0;

    /// <summary>
    /// The default scope for the parallel run. Although method level gives maximum parallelization, the default is set to
    /// class level to enable maximum number of customers to easily convert their tests to run in parallel. In most cases within
    /// a class tests aren't thread safe.
    /// </summary>
    private const ExecutionScope DefaultExecutionScope = ExecutionScope.ClassLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelizeAttribute"/> class.
    /// </summary>
    public ParallelizeAttribute()
    {
        Workers = DefaultParallelWorkers;
        Scope = DefaultExecutionScope;
    }

    /// <summary>
    /// Gets or sets the number of workers to be used for the parallel run.
    /// </summary>
    public int Workers { get; set; }

    /// <summary>
    /// Gets or sets the scope of the parallel run.
    /// </summary>
    /// <remarks>
    /// To enable all classes to run in parallel set this to <see cref="ExecutionScope.ClassLevel"/>.
    /// To get the maximum parallelization level set this to <see cref="ExecutionScope.MethodLevel"/>.
    /// </remarks>
    public ExecutionScope Scope { get; set; }
}
