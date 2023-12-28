// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Scope = ExecutionScope.ClassLevel, Workers = 0)]

namespace Playground;

[TestClass]
public class TestClass
{
    [TestMethod]
    public async Task Test1()
    {
        await Task.Delay(1000);
    }
}
