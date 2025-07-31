// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTest.TestAdapter.UnitTests;

public class DataSerializationHelperTests : TestContainer
{
    public void DataSerializerShouldRoundTripDateTimeOffset()
    {
        var source = new DateTimeOffset(628381323438126060, TimeSpan.FromHours(-8));

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        Verify(actual!.Length == 1);
        Verify(actual[0]!.GetType() == typeof(DateTimeOffset));
        Verify(actual[0]!.Equals(source));
    }

    public void DataSerializerShouldRoundTripDateTime()
    {
        var source = new DateTime(628381323438126060);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        Verify(actual!.Length == 1);
        Verify(actual[0]!.GetType() == typeof(DateTime));
        Verify(actual[0]!.Equals(source));
        Verify(((DateTime)actual[0]!).Kind.Equals(source.Kind));
    }

    public void DataSerializerShouldRoundTripDateTimeOfKindLocal()
    {
        var source = new DateTime(628381323438126060, DateTimeKind.Local);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        Verify(actual!.Length == 1);
        Verify(actual[0]!.GetType() == typeof(DateTime));
        Verify(actual[0]!.Equals(source));
        Verify(((DateTime)actual[0]!).Kind.Equals(source.Kind));
    }

    public void DataSerializerShouldRoundTripDateTimeOfKindUtc()
    {
        var source = new DateTime(628381323438126060, DateTimeKind.Utc);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        Verify(actual!.Length == 1);
        Verify(actual[0]!.GetType() == typeof(DateTime));
        Verify(actual[0]!.Equals(source));
        Verify(((DateTime)actual[0]!).Kind.Equals(source.Kind));
    }

#if NET7_0_OR_GREATER
    public void DataSerializerShouldRoundTripDateOnly()
    {
        var source = new DateOnly(1999, 11, 3);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        Verify(actual!.Length == 1);
        Verify(actual[0]!.GetType() == typeof(DateOnly));
        Verify(actual[0]!.Equals(source));
    }

    public void DataSerializerShouldRoundTripTimeOnly()
    {
        var source = new TimeOnly(hour: 14, minute: 50, second: 13, millisecond: 15);

        object?[]? actual = DataSerializationHelper.Deserialize(DataSerializationHelper.Serialize([source]));

        Verify(actual!.Length == 1);
        Verify(actual[0]!.GetType() == typeof(TimeOnly));
        Verify(actual[0]!.Equals(source));
    }
#endif
}
