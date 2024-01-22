// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public class FileArtifact(FileInfo fileInfo, string displayName, string? description = null)
    : PropertyBagData(displayName, description)
{
    public FileInfo FileInfo { get; } = fileInfo;

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

            builder.Append(' ').Append(property.ToString());
        }

        if (hasAnyProperty)
        {
            builder.Append(' ');
        }

        builder.Append("] }");

        return builder.ToString();
    }
}

public class SessionFileArtifact(SessionUid sessionUid, FileInfo fileInfo, string displayName, string? description = null)
    : DataWithSessionUid(displayName, description, sessionUid)
{
    public FileInfo FileInfo { get; } = fileInfo;

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

            builder.Append(' ').Append(property.ToString());
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

public class TestNodeFileArtifact(SessionUid sessionUid, TestNode testNode, FileInfo fileInfo, string displayName, string? description = null)
    : SessionFileArtifact(sessionUid, fileInfo, displayName, description)
{
    public TestNode TestNode { get; } = testNode;

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

            builder.Append(' ').Append(property.ToString());
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
            .Append(TestNode.ToString())
            .Append(" }");

        return builder.ToString();
    }
}
