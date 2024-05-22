// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Parallel configuration
using Microsoft.VisualStudio.TestTools.UnitTesting;

//[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.ClassLevel)]

namespace ParallelClassesTestProject;

internal class Constants
{
    internal const int WaitTimeInMS = 1000;
}
