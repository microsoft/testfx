// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

[PipeSerializableMessage("RetryFailedTestsProtocol", 4)]
internal sealed record TotalTestsRunRequest(
    [property: PipePropertyId(1)]
    int? TotalTests) : IRequest;
