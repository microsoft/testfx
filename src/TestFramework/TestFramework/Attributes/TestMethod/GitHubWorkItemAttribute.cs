// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// GitHubWorkItem attribute; used to specify a GitHub issue associated with this test.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class GitHubWorkItemAttribute : WorkItemAttribute
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
        => int.TryParse(url.Substring(url.LastIndexOf('/') + 1), out int id)
            ? id
            : throw new ArgumentException("The URL provided is not a valid GitHub ticket URL.", nameof(url));
}
