// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

// A single test trait (key/value metadata) carried over the pipe. This is the wire type shared with the SDK; it is
// deliberately decoupled from the public TestMetadataProperty (which is IProperty/TestNode-coupled) so this model can
// be compiled standalone into a consumer that has no reference to Microsoft.Testing.Platform. The host converts
// TestMetadataProperty -> TraitMessage at the boundary; the wire bytes (TraitMessageFieldsId.Key/.Value) are unchanged.
internal sealed record TraitMessage(string Key, string Value);

internal sealed record DiscoveredTestMessage(string? Uid, string? DisplayName, string? FilePath, int? LineNumber, string? Namespace, string? TypeName, string? MethodName, string[]? ParameterTypeFullNames, TraitMessage[] Traits);

internal sealed record DiscoveredTestMessages(string? ExecutionId, string? InstanceId, DiscoveredTestMessage[] DiscoveredMessages) : IRequest;
