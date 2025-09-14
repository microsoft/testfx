// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.IPC.Models;

internal sealed record DiscoveredTestMessage(string? Uid, string? DisplayName, string? FilePath, int? LineNumber, string? Namespace, string? TypeName, string? MethodName, string[] ParameterTypeFullNames, TestMetadataProperty[] Traits);

internal sealed record DiscoveredTestMessages(string? ExecutionId, string? InstanceId, DiscoveredTestMessage[] DiscoveredMessages) : IRequest;
