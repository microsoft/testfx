// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests.Services;

[TestClass]
public sealed class PlatformInformationTests
{
    [TestMethod]
    public void BuildDate_MatchesAssemblyMetadataAtMidnightUtc()
    {
        string? expectedDate = typeof(IPlatformInformation).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "Microsoft.Testing.Platform.Application.BuildTimeUTC").Value;

        DateTimeOffset? buildDate = new PlatformInformation().BuildDate;

        Assert.IsNotNull(buildDate);
        DateTimeOffset actualDate = buildDate.Value;
        Assert.AreEqual(expectedDate, actualDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
        Assert.AreEqual(TimeSpan.Zero, actualDate.Offset);
        Assert.AreEqual(TimeSpan.Zero, actualDate.TimeOfDay);
    }
}
