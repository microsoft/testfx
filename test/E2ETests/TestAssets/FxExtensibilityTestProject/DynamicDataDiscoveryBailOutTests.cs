// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FxExtensibilityTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System.Collections.Generic;

    [TestClass]
    public class DynamicDataDiscoveryBailOutTests
    {
        public static IEnumerable<object[]> DuplicateDisplayName()
        {
            yield return new object[] { null };
            yield return new object[] { new List<int>() };
            yield return new object[] { new List<int>() { 0, 1, 2 } };
        }

        [TestMethod]
        [DynamicData(nameof(DuplicateDisplayName), DynamicDataSourceType.Method)]
        public void DynamicDataDiscoveryBailOutTestMethod1(IEnumerable<int> items)
        {
        }
    }
}
