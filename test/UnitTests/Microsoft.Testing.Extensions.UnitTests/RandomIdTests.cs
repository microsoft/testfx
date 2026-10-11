#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RandomIdTests
{
    private const int IdLength = 5;
    private const int SampleSize = 200;

    [TestMethod]
    public void Next_ReturnsFiveCharacterId()
    {
        string id = RandomId.Next();

        Assert.HasCount(IdLength, id);
    }

    [TestMethod]
    public void Next_AcrossManyCalls_ReturnsOnlyAlphaNumericCharacters()
    {
        for (int i = 0; i < SampleSize; i++)
        {
            AssertValidId(RandomId.Next());
        }
    }

    [TestMethod]
    public void Next_CalledManyTimes_ReturnsDistinctEnoughIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < SampleSize; i++)
        {
            ids.Add(RandomId.Next());
        }

        Assert.IsGreaterThanOrEqualTo(190, ids.Count);
    }

    [TestMethod]
    public void Next_CalledConcurrently_ReturnsValidIds()
    {
        string[] ids = new string[SampleSize];

        Parallel.For(0, ids.Length, i => ids[i] = RandomId.Next());

        foreach (string id in ids)
        {
            AssertValidId(id);
        }
    }

    private static void AssertValidId(string id)
    {
        Assert.HasCount(IdLength, id);
        foreach (char character in id)
        {
            bool isValidCharacter = character is >= '0' and <= '9'
                or >= 'A' and <= 'Z'
                or >= 'a' and <= 'z';
            Assert.IsTrue(isValidCharacter, $"Unexpected character '{character}' in ID '{id}'.");
        }
    }
}
