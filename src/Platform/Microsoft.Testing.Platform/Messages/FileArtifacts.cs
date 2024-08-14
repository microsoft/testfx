// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

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
    public FileArtifact(FileInfo fileInfo, string displayName, string? description = null)
        : base(displayName, description) => FileInfo = fileInfo;

    /// <summary>
    /// Gets the file information.
    /// </summary>
    public FileInfo FileInfo { get; }

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
    public SessionFileArtifact(SessionUid sessionUid, FileInfo fileInfo, string displayName, string? description = null)
        : base(displayName, description, sessionUid) => FileInfo = fileInfo;

    /// <summary>
    /// Gets the file information.
    /// </summary>
    public FileInfo FileInfo { get; }

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

/// <summary>
/// Represents a test node file artifact.
/// </summary>
public class TestNodeFileArtifact : SessionFileArtifact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestNodeFileArtifact"/> class.
    /// </summary>
    /// <param name="sessionUid">The session UID.</param>
    /// <param name="testNode">The test node.</param>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    public TestNodeFileArtifact(SessionUid sessionUid, TestNode testNode, FileInfo fileInfo, string displayName, string? description = null)
        : base(sessionUid, fileInfo, displayName, description) => TestNode = testNode;

    /// <summary>
    /// Gets the test node.
    /// </summary>
    public TestNode TestNode { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder("TestNodeFileArtifact { DisplayName = ")
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
            .Append(", TestNode = ")
            .Append(TestNode)
            .Append(" }");

        return builder.ToString();
    }
}
