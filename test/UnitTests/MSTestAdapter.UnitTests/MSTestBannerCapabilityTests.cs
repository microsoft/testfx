// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class MSTestBannerCapabilityTests : TestContainer
{
    public async Task GetBannerMessageAsync_IncludesVersionAndBuildDate()
    {
        var buildDate = new DateTimeOffset(2026, 01, 02, 03, 04, 05, TimeSpan.Zero);
        var sut = new MSTestBannerCapability(new PlatformInformationStub(buildDate));

        string? bannerMessage = await sut.GetBannerMessageAsync();

        bannerMessage.Should().NotBeNull();
        bannerMessage.Should().StartWith($"MSTest v{MSTestVersion.SemanticVersion}");
        bannerMessage.Should().Contain($"(UTC {buildDate.UtcDateTime.ToShortDateString()})");
    }

    public async Task GetBannerMessageAsync_DoesNotIncludeBuildDate_WhenBuildDateIsNotAvailable()
    {
        var sut = new MSTestBannerCapability(new PlatformInformationStub(null));

        string? bannerMessage = await sut.GetBannerMessageAsync();

        bannerMessage.Should().NotBeNull();
        bannerMessage.Should().StartWith($"MSTest v{MSTestVersion.SemanticVersion}");
        bannerMessage.Should().NotContain("(UTC ");
    }

    private sealed class PlatformInformationStub(DateTimeOffset? buildDate) : IPlatformInformation
    {
        public string Name => "Test";

        public DateTimeOffset? BuildDate => buildDate;

        public string? Version => null;

        public string? CommitHash => null;
    }
}
