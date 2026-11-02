// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TypeNameHelperTests
{
    [TestMethod]
    [DataRow(typeof(void), "void")]
    [DataRow(typeof(bool), "bool")]
    [DataRow(typeof(byte), "byte")]
    [DataRow(typeof(char), "char")]
    [DataRow(typeof(decimal), "decimal")]
    [DataRow(typeof(double), "double")]
    [DataRow(typeof(float), "float")]
    [DataRow(typeof(int), "int")]
    [DataRow(typeof(long), "long")]
    [DataRow(typeof(object), "object")]
    [DataRow(typeof(sbyte), "sbyte")]
    [DataRow(typeof(short), "short")]
    [DataRow(typeof(string), "string")]
    [DataRow(typeof(uint), "uint")]
    [DataRow(typeof(ulong), "ulong")]
    [DataRow(typeof(ushort), "ushort")]
    [DataRow(typeof(int[,]), "int[,]")]
    [DataRow(typeof(string[][]), "string[][]")]
    [DataRow(typeof(Dictionary<string, List<int[,]>>), "System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int[,]>>")]
    public void GetTypeDisplayName_PreservesBuiltinsArrayRanksAndGenericModes(Type type, string expected)
        => Assert.AreEqual(expected, TypeNameHelper.GetTypeDisplayName(type));

    [TestMethod]
    public void GetTypeDisplayName_NestedGenericOffsetsAndDelimiterAreExact()
    {
        Assert.AreEqual(
            "Microsoft.Testing.Platform.UnitTests.TypeNameHelperTests.GenericContainer<int>.Nested<string>",
            TypeNameHelper.GetTypeDisplayName(typeof(GenericContainer<int>.Nested<string>), nestedTypeDelimiter: '.'));
        Assert.AreEqual("Nested<string>", TypeNameHelper.GetTypeDisplayName(typeof(GenericContainer<int>.Nested<string>), fullName: false));
        Assert.AreEqual("Nested", TypeNameHelper.GetTypeDisplayName(typeof(GenericContainer<int>.Nested<string>), fullName: false, includeGenericParameters: false));
        Assert.AreEqual(
            "Microsoft.Testing.Platform.UnitTests.TypeNameHelperTests.PlainContainer.Leaf",
            TypeNameHelper.GetTypeDisplayName(typeof(PlainContainer.Leaf), nestedTypeDelimiter: '.'));
        Assert.AreEqual(
            "Microsoft.Testing.Platform.UnitTests.TypeNameHelperTests.PlainContainer.Leaf[]",
            TypeNameHelper.GetTypeDisplayName(typeof(PlainContainer.Leaf[]), nestedTypeDelimiter: '.'));
        Assert.AreEqual("Leaf[]", TypeNameHelper.GetTypeDisplayName(typeof(PlainContainer.Leaf[]), fullName: false));
    }

    [TestMethod]
    public void GetTypeDisplayName_GenericParameterVisibilityIsRespectedInsideArrays()
    {
        Type parameter = typeof(Dictionary<,>).GetGenericArguments()[0];

        Assert.AreEqual(string.Empty, TypeNameHelper.GetTypeDisplayName(parameter));
        Assert.AreEqual("TKey", TypeNameHelper.GetTypeDisplayName(parameter, includeGenericParameterNames: true));
        Assert.AreEqual("[,]", TypeNameHelper.GetTypeDisplayName(parameter.MakeArrayType(2)));
        Assert.AreEqual("TKey[,]", TypeNameHelper.GetTypeDisplayName(parameter.MakeArrayType(2), includeGenericParameterNames: true));
        Assert.AreEqual("Dictionary<,>", TypeNameHelper.GetTypeDisplayName(typeof(Dictionary<,>), fullName: false));
        Assert.AreEqual("Dictionary<TKey, TValue>", TypeNameHelper.GetTypeDisplayName(typeof(Dictionary<,>), fullName: false, includeGenericParameterNames: true));
        Assert.AreEqual("Dictionary", TypeNameHelper.GetTypeDisplayName(typeof(Dictionary<,>), fullName: false, includeGenericParameters: false));
    }

    private sealed class GenericContainer<TOuter>
    {
        public sealed class Nested<TInner>;
    }

    private sealed class PlainContainer
    {
        public sealed class Leaf;
    }
}
