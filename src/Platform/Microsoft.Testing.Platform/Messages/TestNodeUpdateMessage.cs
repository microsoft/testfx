// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Extensions.Messages;

public record TestNodeUpdateMessage(SessionUid SessionUid, TestNode TestNode, TestNodeUid? ParentTestNodeUid = null)
    : DataWithSessionUid(nameof(TestNodeUpdateMessage), "This data is used to report a TestNode state change.", SessionUid);
