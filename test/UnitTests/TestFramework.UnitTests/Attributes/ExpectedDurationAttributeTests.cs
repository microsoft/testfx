// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class ExpectedDurationAttributeTests : TestContainer
{
    public void Constructor_NonPositiveMilliseconds_ThrowsArgumentOutOfRangeException()
    {
        foreach (int milliseconds in new[] { 0, -1, int.MinValue })
        {
            Action act = () => _ = new ExpectedDurationAttribute(milliseconds);

            act.Should().ThrowExactly<ArgumentOutOfRangeException>()
                .WithParameterName("milliseconds");
        }
    }
}
