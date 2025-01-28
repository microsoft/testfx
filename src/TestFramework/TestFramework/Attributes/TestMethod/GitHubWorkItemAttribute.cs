// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// GitHubWorkItem attribute; used to specify a GitHub issue associated with this test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed partial class GitHubWorkItemAttribute : WorkItemAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubWorkItemAttribute"/> class for the GitHub WorkItem Attribute.
    /// </summary>
    /// <param name="url">The URL to a GitHub issue.</param>
    public GitHubWorkItemAttribute(string url)
        : base(ExtractId(url))
        => Url = url;

    /// <summary>
    /// Gets the URL to the GitHub issue associated.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Extracts the ID from the GitHub issue/pull/discussion URL.
    /// </summary>
    /// <param name="url">The URL to a GitHub ticket.</param>
    /// <returns>The ticket ID.</returns>
    private static int ExtractId(string url)
    {
#if NET7_0_OR_GREATER
        Match match = GitHubTicketRegex().Match(url);
#else
        Match match = Regex.Match(url, @"https:\/\/github\.com\/.+\/.+\/(issues|pull|discussions)\/(\d+)(#.+)?");
#endif
        return match.Success && int.TryParse(match.Groups[2].Value, out int issueId)
            ? issueId
            : throw new ArgumentException(FrameworkMessages.InvalidGitHubUrl, nameof(url));
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex("https:\\/\\/github\\.com\\/.+\\/.+\\/(issues|pull|discussions)\\/(\\d+)(#.+)?")]
    private static partial Regex GitHubTicketRegex();
#endif
}
