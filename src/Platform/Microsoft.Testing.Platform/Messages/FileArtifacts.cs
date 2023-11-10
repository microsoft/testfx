// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public class FileArtifact(FileInfo fileInfo, string displayName, string? description = null)
    : PropertyBagData(displayName, description)
{
    public FileInfo FileInfo { get; } = fileInfo;
}

public class SessionFileArtifact(SessionUid sessionUid, FileInfo fileInfo, string displayName, string? description = null)
    : DataWithSessionUid(displayName, description, sessionUid)
{
    public FileInfo FileInfo { get; } = fileInfo;
}

public class TestNodeFileArtifact(SessionUid sessionUid, TestNode node, FileInfo fileInfo, string displayName, string? description = null)
    : SessionFileArtifact(sessionUid, fileInfo, displayName, description)
{
    public TestNode Node { get; } = node;
}
