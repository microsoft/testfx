// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

// Use a fixed, explicit worker count (rather than Workers = 0, which resolves to
// Environment.ProcessorCount) so the three tests in each class are guaranteed to be
// scheduled on separate workers regardless of the CI agent's core count. The tests
// only sleep, so they overlap through concurrency even on a single-core machine, which
// is what the OutputTests parallel-output assertions rely on.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 4)]
