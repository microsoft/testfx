// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Framework.UnitTests;

/// <summary>
/// This class uses DynamicData, to prove that running such tests works.
/// </summary>
[TestClass]
public class DynamicDataTests
{
    public static IEnumerable<object[]> IntDataProperty
        =>
        [
            [1, 2],
            [2, 3]
        ];

    [DynamicData(nameof(IntDataProperty))]
    [TestMethod]
    public void DynamicDataWithIntProperty(int expected, int actualPlus1)
        => Assert.AreEqual(expected, actualPlus1 - 1);

    [DynamicData(nameof(IntDataProperty))]
    [TestMethod]
    public void DynamicDataWithIntPropertyAndExplicitSourceType(int expected, int actualPlus1)
        => Assert.AreEqual(expected, actualPlus1 - 1);

    [DynamicData(nameof(IntDataMethod))]
    [TestMethod]
    public void DynamicDataWithIntMethod(int expected, int actualPlus1)
        => Assert.AreEqual(expected, actualPlus1 - 1);

    [DynamicData(nameof(IntDataMethod))]
    [TestMethod]
    public void DynamicDataWithIntMethodAndExplicitSourceType(int expected, int actualPlus1)
        => Assert.AreEqual(expected, actualPlus1 - 1);

    public static IEnumerable<object[]> IntDataMethod()
        =>
        [
            [1, 2],
            [2, 3]
        ];

    [DynamicData(nameof(IntDataProperty), typeof(DataClass))]
    [TestMethod]
    public void DynamicDataWithIntPropertyOnSeparateClass(int expected, int actualPlus2)
        => Assert.AreEqual(expected, actualPlus2 - 2);

    [DynamicData(nameof(IntDataMethod), typeof(DataClass))]
    [TestMethod]
    public void DynamicDataWithIntMethodOnSeparateClass(int expected, int actualPlus2)
        => Assert.AreEqual(expected, actualPlus2 - 2);

    [DynamicData(nameof(UserDataProperty))]
    [TestMethod]
    public void DynamicDataWithUserProperty(User _, User _2)
    {
    }

    public static IEnumerable<object[]> UserDataProperty
        =>
        [
            [new User("Jakub"), new User("Amaury")],
            [new User("Marco"), new User("Pavel")]
        ];

    [DynamicData(nameof(UserDataMethod))]
    [TestMethod]
    public void DynamicDataWithUserMethod(User _, User _2)
    {
    }

    public static IEnumerable<object[]> UserDataMethod()
        =>
        [
            [new User("Jakub"), new User("Amaury")],
            [new User("Marco"), new User("Pavel")]
        ];
}

public class DataClass
{
    public static IEnumerable<object[]> IntDataProperty
        =>
        [
            [1, 3],
            [2, 4]
        ];

    public static IEnumerable<object[]> IntDataMethod()
        =>
        [
            [1, 3],
            [2, 4]
        ];
}

public class User
{
    public User(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
