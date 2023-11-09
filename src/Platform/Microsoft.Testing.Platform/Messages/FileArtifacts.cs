// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public record FileArtifact(FileInfo FileInfo, string DisplayName, string? Description = null)
    : PropertyBagData(DisplayName, Description);

public record SessionFileArtifact(SessionUid SessionUid, FileInfo FileInfo, string DisplayName, string? Description = null)
    : DataWithSessionUid(DisplayName, Description, SessionUid);

public record TestNodeFileArtifact(SessionUid SessionUid, TestNode Node, FileInfo FileInfo, string DisplayName, string? Description = null)
    : SessionFileArtifact(SessionUid, FileInfo, DisplayName, Description);
