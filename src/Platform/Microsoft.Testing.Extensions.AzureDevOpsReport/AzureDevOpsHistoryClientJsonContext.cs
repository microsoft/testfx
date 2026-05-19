// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

[JsonSerializable(typeof(AzureDevOpsResultsResponse))]
[JsonSerializable(typeof(AzureDevOpsRunsResponse))]
internal sealed partial class AzureDevOpsHistoryClientJsonContext : JsonSerializerContext;
