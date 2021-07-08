// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

    using TestableImplementations;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DataSerializationHelperTests
    {
        [TestMethod]
        public void DataSerializerShouldRoundTripDateTimeOffset()
        {
            var source = new DateTimeOffset(628381323438126060, TimeSpan.FromHours(-8));

            var actual = DataSerializationHelper.Deserialize(
                DataSerializationHelper.Serialize(new object[] { source }));

            actual.Should().HaveCount(1);
            actual[0].Should().BeOfType<DateTimeOffset>();
            actual[0].As<DateTimeOffset>().Should().Be(source);
        }

        [TestMethod]
        public void DataSerializerShouldRoundTripDateTime()
        {
            var source = new DateTime(628381323438126060);

            var actual = DataSerializationHelper.Deserialize(
                DataSerializationHelper.Serialize(new object[] { source }));

            actual.Should().HaveCount(1);
            actual[0].Should().BeOfType<DateTime>();
            actual[0].As<DateTime>().Should().Be(source);
            actual[0].As<DateTime>().Kind.Should().Be(source.Kind);
        }

        [TestMethod]
        public void DataSerializerShouldRoundTripDateTimeOfKindLocal()
        {
            var source = new DateTime(628381323438126060, DateTimeKind.Local);

            var actual = DataSerializationHelper.Deserialize(
                DataSerializationHelper.Serialize(new object[] { source }));

            actual.Should().HaveCount(1);
            actual[0].Should().BeOfType<DateTime>();
            actual[0].As<DateTime>().Should().Be(source);
            actual[0].As<DateTime>().Kind.Should().Be(source.Kind);
        }

        [TestMethod]
        public void DataSerializerShouldRoundTripDateTimeOfKindUtc()
        {
            var source = new DateTime(628381323438126060, DateTimeKind.Utc);

            var actual = DataSerializationHelper.Deserialize(
                DataSerializationHelper.Serialize(new object[] { source }));

            actual.Should().HaveCount(1);
            actual[0].Should().BeOfType<DateTime>();
            actual[0].As<DateTime>().Should().Be(source);
            actual[0].As<DateTime>().Kind.Should().Be(source.Kind);
        }
    }
}
