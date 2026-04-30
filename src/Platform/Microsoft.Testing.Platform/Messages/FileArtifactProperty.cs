// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>
/// Property that represents multiple artifacts/attachments to associate with a test node.
/// </summary>
public sealed class FileArtifactProperty : IProperty, IEquatable<FileArtifactProperty>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileArtifactProperty"/> class.
    /// </summary>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="description">The description.</param>
    public FileArtifactProperty(FileInfo fileInfo, string displayName, string? description = null)
    {
        FileInfo = fileInfo;
        DisplayName = displayName;
        Description = description;
    }

    /// <summary>
    /// Gets the file information.
    /// </summary>
    public FileInfo FileInfo { get; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string? Description { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(nameof(FileArtifactProperty));
        builder.Append(" { ");
        builder.Append($"{nameof(FileInfo)} = ");
        builder.Append(FileInfo);
        builder.Append($", {nameof(DisplayName)} = ");
        builder.Append(DisplayName);
        builder.Append($", {nameof(Description)} = ");
        builder.Append(Description);
        builder.Append(" }");
        return builder.ToString();
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => Equals(obj as FileArtifactProperty);

    /// <inheritdoc />
    public bool Equals(FileArtifactProperty? other)
        => other is not null && Equals(FileInfo, other.FileInfo) && DisplayName == other.DisplayName && Description == other.Description;

    /// <inheritdoc />
    public override int GetHashCode()
        => RoslynHashCode.Combine(FileInfo, DisplayName, Description);
}
