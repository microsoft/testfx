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

    [TestMethod]
    public void PlatformIdentity_AdvertisesTheOwningAssemblyVersionAndCommit()
    {
        string manifestVersion = typeof(IPlatformInformation).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        string[] components = manifestVersion.Split('+');
        Assert.IsTrue(components.Length is 1 or 2);
        PlatformInformation platform = new();

        Assert.AreEqual("Microsoft.Testing.Platform", platform.Name);
        Assert.AreEqual(components[0], platform.Version);
        Assert.AreEqual(components.Length == 2 ? components[1] : null, platform.CommitHash);
#pragma warning disable CS0618 // The retained compatibility value must match the shipping platform version.
        Assert.AreEqual(components[0], Microsoft.Testing.Platform.Helpers.AppVersion.DefaultSemVer);
#pragma warning restore CS0618
    }
}
