// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class GitHubWorkItemTests : TestContainer
{
    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_IssueUrl()
    {
        string url = "https://github.com/microsoft/testfx/issues/1234";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_IssueUrlWithEndingSlash()
    {
        string url = "https://github.com/microsoft/testfx/issues/1234/";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_IssueUrlWithComment()
    {
        string url = "https://github.com/microsoft/testfx/issues/1234#issuecomment-2581012838";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_PRUrl()
    {
        string url = "https://github.com/microsoft/testfx/pull/1234";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_PRUrlWithEndingSlash()
    {
        string url = "https://github.com/microsoft/testfx/pull/1234/";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_PRUrlWithComment()
    {
        string url = "https://github.com/microsoft/testfx/pull/1234#discussion_r1932733213";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_DiscussionUrl()
    {
        string url = "https://github.com/microsoft/testfx/discussions/1234";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_DiscussionUrlWithEndingSlash()
    {
        string url = "https://github.com/microsoft/testfx/discussions/1234/";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }

    public void GitHubWorkItemAttributeShouldExtractIdFromUrl_DiscussionUrlWithComment()
    {
        string url = "https://github.com/microsoft/testfx/discussions/1234#discussioncomment-11865020";
        GitHubWorkItemAttribute attribute = new(url);
        attribute.Url.Should().Be(url);
        attribute.Id.Should().Be(1234);
    }
}
