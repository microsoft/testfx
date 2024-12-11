// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DiscoverInternals]

namespace DiscoverInternalsProject;

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

internal class FancyString;

public abstract class CaseInsensitivityTests<T>
{
    protected abstract Tuple<T, T> EquivalentInstancesDistinctInCase { get; }

    [TestMethod]
    public void EqualityIsCaseInsensitive()
    {
        Tuple<T, T> tuple = EquivalentInstancesDistinctInCase;

        Assert.AreEqual(tuple.Item1, tuple.Item2);
    }
}

[TestClass]
internal class FancyStringsAreCaseInsensitive : CaseInsensitivityTests<FancyString>
{
    protected override Tuple<FancyString, FancyString> EquivalentInstancesDistinctInCase =>
        new(new FancyString(), new FancyString());
}

[DataContract]
internal sealed class SerializableInternalType;

[TestClass]
internal class DynamicDataTest
{
    [DataTestMethod]
    [DynamicData(nameof(DynamicData))]
    internal void DynamicDataTestMethod(SerializableInternalType serializableInternalType)
    {
    }

    public static IEnumerable<object[]> DynamicData =>
    [
        [new SerializableInternalType()]
    ];
}
