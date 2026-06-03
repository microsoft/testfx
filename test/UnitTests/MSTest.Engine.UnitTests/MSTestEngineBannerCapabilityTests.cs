// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Framework.UnitTests;

[TestClass]
public sealed class MSTestEngineBannerCapabilityTests
{
    [TestMethod]
    public async Task GetBannerMessageAsync_IncludesVersionAndBuildDate()
    {
        var buildDate = new DateTimeOffset(2026, 01, 02, 03, 04, 05, TimeSpan.Zero);
        var sut = new MSTestEngineBannerCapability(new PlatformInformationStub(buildDate));

        string? bannerMessage = await sut.GetBannerMessageAsync();

        Assert.IsNotNull(bannerMessage);
        StringAssert.StartsWith(bannerMessage, $"MSTest.Engine v{MSTestEngineRepositoryVersion.Version}");
        StringAssert.Contains(bannerMessage, $"(UTC {buildDate.UtcDateTime.ToShortDateString()})");
    }

    [TestMethod]
    public async Task GetBannerMessageAsync_DoesNotIncludeBuildDate_WhenBuildDateIsNotAvailable()
    {
        var sut = new MSTestEngineBannerCapability(new PlatformInformationStub(null));

        string? bannerMessage = await sut.GetBannerMessageAsync();

        Assert.IsNotNull(bannerMessage);
        StringAssert.StartsWith(bannerMessage, $"MSTest.Engine v{MSTestEngineRepositoryVersion.Version}");
        Assert.IsFalse(bannerMessage.Contains("(UTC ", StringComparison.Ordinal));
    }

    private sealed class PlatformInformationStub(DateTimeOffset? buildDate) : IPlatformInformation
    {
        public string Name => "Test";

        public DateTimeOffset? BuildDate => buildDate;

        public string? Version => null;

        public string? CommitHash => null;
    }
}
