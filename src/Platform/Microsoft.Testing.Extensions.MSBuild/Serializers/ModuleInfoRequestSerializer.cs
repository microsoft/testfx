// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

internal sealed record ModuleInfoRequest(string FrameworkDescription, string ProcessArchitecture, string TestResultFolder) : IRequest;

internal sealed class ModuleInfoRequestSerializer : NamedPipeSerializer<ModuleInfoRequest>, INamedPipeSerializer
{
    public override int Id => 1;

    protected override ModuleInfoRequest DeserializeCore(Stream stream)
        => new ModuleInfoRequest(ReadString(stream), ReadString(stream), ReadString(stream));

    protected override void SerializeCore(ModuleInfoRequest objectToSerialize, Stream stream)
    {
        WriteString(stream, objectToSerialize.FrameworkDescription);
        WriteString(stream, objectToSerialize.ProcessArchitecture);
        WriteString(stream, objectToSerialize.TestResultFolder);
    }
}
