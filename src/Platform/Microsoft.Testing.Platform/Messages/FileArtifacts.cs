// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Represents a file artifact.
/// </summary>
public class FileArtifact : PropertyBagData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileArtifact"/> class.
    /// </summary>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    // This shipped constructor intentionally keeps its optional 'description' parameter for binary
    // compatibility; the newer overload below adds 'kind'. Folding them into a single constructor
    // would binary-break existing compiled callers of this 3-parameter overload, so RS0027 (which
    // wants the optional-parameter overload to have the most parameters) is suppressed here.
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    public FileArtifact(FileInfo fileInfo, string displayName, string? description = null)
#pragma warning restore RS0027
        : base(displayName, description) => FileInfo = fileInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileArtifact"/> class with a producer-asserted artifact kind.
    /// </summary>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    /// <param name="kind">
    /// An optional producer-asserted, reverse-DNS identifier of the artifact format
    /// (e.g. <c>microsoft.testing.trx</c>). Used by post-processing to group artifacts of
    /// the same kind for consolidation. <see langword="null"/> when the producer does not
    /// declare a kind.
    /// </param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public FileArtifact(FileInfo fileInfo, string displayName, string? description, string? kind)
        : base(displayName, description)
    {
        FileInfo = fileInfo;
        Kind = kind;
    }

    /// <summary>
    /// Gets the file information.
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// Gets the producer-asserted, reverse-DNS identifier of the artifact format
    /// (e.g. <c>microsoft.testing.trx</c>), or <see langword="null"/> when the producer
    /// did not declare one.
    /// </summary>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public string? Kind { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("FileArtifact { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
            .Append(", FilePath = ")
            .Append(FileInfo.FullName)
            .Append(", Properties = [");

        bool hasAnyProperty = false;
        foreach (IProperty property in Properties)
        {
            if (!hasAnyProperty)
            {
                hasAnyProperty = true;
            }
            else
            {
                builder.Append(',');
            }

            builder.Append(' ').Append(property);
        }

        if (hasAnyProperty)
        {
            builder.Append(' ');
        }

        builder.Append("] }");

        return builder.ToString();
    }
}

/// <summary>
/// Represents a session file artifact.
/// </summary>
public class SessionFileArtifact : DataWithSessionUid
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionFileArtifact"/> class.
    /// </summary>
    /// <param name="sessionUid">The session UID.</param>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    // This shipped constructor intentionally keeps its optional 'description' parameter for binary
    // compatibility; the newer overload below adds 'kind'. Folding them into a single constructor
    // would binary-break existing compiled callers of this 4-parameter overload, so RS0027 (which
    // wants the optional-parameter overload to have the most parameters) is suppressed here.
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    public SessionFileArtifact(SessionUid sessionUid, FileInfo fileInfo, string displayName, string? description = null)
#pragma warning restore RS0027
        : base(displayName, description, sessionUid) => FileInfo = fileInfo;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionFileArtifact"/> class with a producer-asserted artifact kind.
    /// </summary>
    /// <param name="sessionUid">The session UID.</param>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    /// <param name="kind">
    /// An optional producer-asserted, reverse-DNS identifier of the artifact format
    /// (e.g. <c>microsoft.testing.trx</c>). Used by post-processing to group artifacts of
    /// the same kind for consolidation. <see langword="null"/> when the producer does not
    /// declare a kind.
    /// </param>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public SessionFileArtifact(SessionUid sessionUid, FileInfo fileInfo, string displayName, string? description, string? kind)
        : base(displayName, description, sessionUid)
    {
        FileInfo = fileInfo;
        Kind = kind;
    }

    /// <summary>
    /// Gets the file information.
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// Gets the producer-asserted, reverse-DNS identifier of the artifact format
    /// (e.g. <c>microsoft.testing.trx</c>), or <see langword="null"/> when the producer
    /// did not declare one.
    /// </summary>
    [Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
    public string? Kind { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("SessionFileArtifact { DisplayName = ")
            .Append(DisplayName)
            .Append(", Description = ")
            .Append(Description)
            .Append(", FilePath = ")
            .Append(FileInfo.FullName)
            .Append(", Properties = [");

        bool hasAnyProperty = false;
        foreach (IProperty property in Properties)
        {
            if (!hasAnyProperty)
            {
                hasAnyProperty = true;
            }
            else
            {
                builder.Append(',');
            }

            builder.Append(' ').Append(property);
        }

        if (hasAnyProperty)
        {
            builder.Append(' ');
        }

        builder.Append("], SessionUid =")
            .Append(SessionUid.Value)
            .Append(", FilePath = ")
            .Append(FileInfo.FullName)
            .Append(" }");

        return builder.ToString();
    }
}
