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
        StringBuilder builder = new();
        builder.AppendLine("Test node file artifact:");
        builder.Append("Display name: ").AppendLine(DisplayName);
        builder.Append("Description: ").AppendLine(Description);
        builder.Append("File path: ").AppendLine(FileInfo.FullName);
        builder.AppendLine("Properties: [");
        foreach (IProperty property in Properties)
        {
            builder.AppendLine(property.ToString());
        }

        builder.AppendLine("]");

        return builder.ToString();
    }
}

public class SessionFileArtifact(SessionUid sessionUid, FileInfo fileInfo, string displayName, string? description = null)
    : DataWithSessionUid(displayName, description, sessionUid)
{
    public FileInfo FileInfo { get; } = fileInfo;

    public override string ToString()
    {
        StringBuilder builder = new();
        builder.AppendLine("Session file artifact:");
        builder.Append("Display name: ").AppendLine(DisplayName);
        builder.Append("Description: ").AppendLine(Description);
        builder.AppendLine("Properties: [");
        foreach (IProperty property in Properties)
        {
            builder.AppendLine(property.ToString());
        }

        builder.AppendLine("]");
        builder.Append("Session UID: ").AppendLine(SessionUid.Value);
        builder.Append("File path: ").AppendLine(FileInfo.FullName);

        return builder.ToString();
    }
}

public class TestNodeFileArtifact(SessionUid sessionUid, TestNode testNode, FileInfo fileInfo, string displayName, string? description = null)
    : SessionFileArtifact(sessionUid, fileInfo, displayName, description)
{
    public TestNode TestNode { get; } = testNode;

    public override string ToString()
    {
        StringBuilder builder = new();
        builder.AppendLine("Session test node file artifact:");
        builder.Append("Display name: ").AppendLine(DisplayName);
        builder.Append("Description: ").AppendLine(Description);
        builder.AppendLine("Properties: [");
        foreach (IProperty property in Properties)
        {
            builder.AppendLine(property.ToString());
        }

        builder.AppendLine("]");
        builder.Append("Session UID: ").AppendLine(SessionUid.Value);
        builder.Append("File path: ").AppendLine(FileInfo.FullName);
        builder.AppendLine("Test node: ").AppendLine("{").AppendLine(TestNode.ToString()).AppendLine("}");

        return builder.ToString();
    }
}
