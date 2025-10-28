// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes;

using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

[PipeSerializableMessage("MSBuildProtocol", 1)]
internal sealed record ModuleInfoRequest(
    [property: PipePropertyId(1)]
    string FrameworkDescription,
    [property: PipePropertyId(2)]
    string ProcessArchitecture,
    [property: PipePropertyId(3)]
    string TestResultFolder) : IRequest;
