// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Framework.UnitTests;

/// <summary>
/// This class uses DataRows, to prove that running such tests works.
/// </summary>
[TestClass]
public class DataRowTests
{
    [DataRow(1, 2)]
    [DataRow(1000000, 3)]
    [TestMethod]
    public void DataRowDataAreConsumed(int expected, int actualPlus1)
        => Assert.AreEqual(expected, actualPlus1 - 1);
}
