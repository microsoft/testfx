// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.Serialization;

[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DiscoverInternals]

namespace DiscoverInternalsProject
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    internal class TopLevelInternalClass
    {
        [TestMethod]
        public void TopLevelInternalClass_TestMethod1()
        {
        }

        [TestClass]
        internal class NestedInternalClass
        {
            [TestMethod]
            public void NestedInternalClass_TestMethod1()
            {
            }
        }
    }

    internal class FancyString
    {
    }

    public abstract class CaseInsensitivityTests<T>
    {
        protected abstract Tuple<T, T> EquivalentInstancesDistinctInCase { get; }

        [TestMethod]
        public void EqualityIsCaseInsensitive()
        {
            var tuple = EquivalentInstancesDistinctInCase;

            Assert.AreEqual(tuple.Item1, tuple.Item2);
        }
    }

    [TestClass]
    internal class FancyStringsAreCaseInsensitive : CaseInsensitivityTests<FancyString>
    {
        protected override Tuple<FancyString, FancyString> EquivalentInstancesDistinctInCase =>
            new Tuple<FancyString, FancyString>(new FancyString(), new FancyString());
    }

    [DataContract]
    internal class SerializableInternalType
    {
    }

    [TestClass]
    internal class DynamicDataTest
    {
        [DataTestMethod]
        [DynamicData(nameof(DynamicData), DynamicDataSourceType.Property)]
        internal void DynamicDataTestMethod(SerializableInternalType serializableInternalType)
        {

        }

        public static IEnumerable<object[]> DynamicData => new[]
        {
            new object[] { new SerializableInternalType() }
        };
    }
}
