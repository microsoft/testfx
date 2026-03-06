// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NUnit.Framework;

[assembly: Parallelizable(ParallelScope.All)]

namespace NUnitPlayground;

[TestFixture]
public class TestClass
{
    [Test]
    public void Test([Values("one", "one")] string value)
    {
        Thread.Sleep(1000);
    }
}
