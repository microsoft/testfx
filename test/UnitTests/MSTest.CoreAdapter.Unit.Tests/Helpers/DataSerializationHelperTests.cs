﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

using System;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

using TestableImplementations;

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

public class DataSerializationHelperTests : TestContainer
{
    public void DataSerializerShouldRoundTripDateTimeOffset()
    {
        var source = new DateTimeOffset(628381323438126060, TimeSpan.FromHours(-8));

        var actual = DataSerializationHelper.Deserialize(
            DataSerializationHelper.Serialize(new object[] { source }));

        Verify(actual.Length == 1);
        Verify(actual[0].GetType() == typeof(DateTimeOffset));
        Verify(actual[0].Equals(source));
    }

    public void DataSerializerShouldRoundTripDateTime()
    {
        var source = new DateTime(628381323438126060);

        var actual = DataSerializationHelper.Deserialize(
            DataSerializationHelper.Serialize(new object[] { source }));

        Verify(actual.Length == 1);
        Verify(actual[0].GetType() == typeof(DateTime));
        Verify(actual[0].Equals(source));
        Verify(((DateTime)actual[0]).Kind.Equals(source.Kind));
    }

    public void DataSerializerShouldRoundTripDateTimeOfKindLocal()
    {
        var source = new DateTime(628381323438126060, DateTimeKind.Local);

        var actual = DataSerializationHelper.Deserialize(
            DataSerializationHelper.Serialize(new object[] { source }));

        Verify(actual.Length == 1);
        Verify(actual[0].GetType() == typeof(DateTime));
        Verify(actual[0].Equals(source));
        Verify(((DateTime)actual[0]).Kind.Equals(source.Kind));
    }

    public void DataSerializerShouldRoundTripDateTimeOfKindUtc()
    {
        var source = new DateTime(628381323438126060, DateTimeKind.Utc);

        var actual = DataSerializationHelper.Deserialize(
            DataSerializationHelper.Serialize(new object[] { source }));

        Verify(actual.Length == 1);
        Verify(actual[0].GetType() == typeof(DateTime));
        Verify(actual[0].Equals(source));
        Verify(((DateTime)actual[0]).Kind.Equals(source.Kind));
    }
}
