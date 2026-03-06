// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NUnit.Framework;

namespace NUnitPlayground;

[TestFixture]
public class TestClass
{
    [Test]
    public void Test1()
        => Assert.That(true, Is.True);
}
