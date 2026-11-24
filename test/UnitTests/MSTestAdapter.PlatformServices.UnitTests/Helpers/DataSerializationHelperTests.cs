// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class DataSerializationHelperTests : TestContainer
{
    public void DataSerializerShouldRoundTripDateTimeOffset()
    {
        var source = new DateTimeOffset(628381323438126060, TimeSpan.FromHours(-8));

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        actual!.Length.Should().Be(1);
        actual[0].Should().BeOfType<DateTimeOffset>();
        actual[0]!.Equals(source).Should().BeTrue();
    }

    public void DataSerializerShouldRoundTripDateTime()
    {
        var source = new DateTime(628381323438126060);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        actual!.Length.Should().Be(1);
        actual[0].Should().BeOfType<DateTime>();
        actual[0]!.Equals(source).Should().BeTrue();
        ((DateTime)actual[0]!).Kind.Equals(source.Kind).Should().BeTrue();
    }

    public void DataSerializerShouldRoundTripDateTimeOfKindLocal()
    {
        var source = new DateTime(628381323438126060, DateTimeKind.Local);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        actual!.Length.Should().Be(1);
        actual[0].Should().BeOfType<DateTime>();
        actual[0]!.Equals(source).Should().BeTrue();
        ((DateTime)actual[0]!).Kind.Equals(source.Kind).Should().BeTrue();
    }

    public void DataSerializerShouldRoundTripDateTimeOfKindUtc()
    {
        var source = new DateTime(628381323438126060, DateTimeKind.Utc);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        actual!.Length.Should().Be(1);
        actual[0].Should().BeOfType<DateTime>();
        actual[0]!.Equals(source).Should().BeTrue();
        ((DateTime)actual[0]!).Kind.Equals(source.Kind).Should().BeTrue();
    }

#if NET7_0_OR_GREATER
    public void DataSerializerShouldRoundTripDateOnly()
    {
        var source = new DateOnly(1999, 11, 3);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        actual!.Length.Should().Be(1);
        actual[0].Should().BeOfType<DateOnly>();
        actual[0]!.Equals(source).Should().BeTrue();
    }

    public void DataSerializerShouldRoundTripTimeOnly()
    {
        var source = new TimeOnly(hour: 14, minute: 50, second: 13, millisecond: 15);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        actual!.Length.Should().Be(1);
        actual[0].Should().BeOfType<TimeOnly>();
        actual[0]!.Equals(source).Should().BeTrue();
    }
#endif

    public void SerializationPreservesNullSlotsAndExactScalarWireValues()
    {
        DataSerializationHelper.Serialize(null).Should().BeNull();
        DataSerializationHelper.Serialize([]).Should().BeEmpty();

        string?[] serialized = DataSerializationHelper.Serialize([null, 17, "row"])!;

        serialized.Should().Equal(null, null, typeof(int).AssemblyQualifiedName, "17", typeof(string).AssemblyQualifiedName, "\"row\"");
    }

    public void DeserializationRejectsOddPayloadsAndPreservesNullPairs()
    {
        DataSerializationHelper.Deserialize(null).Should().BeNull();
        DataSerializationHelper.Deserialize(["unpaired"]).Should().BeNull();
        DataSerializationHelper.Deserialize([]).Should().BeEmpty();
        DataSerializationHelper.Deserialize([null, "17", typeof(int).AssemblyQualifiedName, null, null, null])
            .Should().Equal(new object?[] { null, null, null });
    }

    public void IndependentJsonPayloadCreatesUncachedSerializer()
    {
        object?[] values = DataSerializationHelper.Deserialize(
            [typeof(long).AssemblyQualifiedName, "9223372036854775806", "UnknownCoverageType", "\"fallback\""])!;

        values.Should().Equal(9223372036854775806L, "fallback");
    }
}
