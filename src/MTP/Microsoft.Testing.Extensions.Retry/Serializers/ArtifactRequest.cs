// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;

internal sealed class ArtifactRequest(string path, string? kind) : IRequest
{
    public string Path { get; } = path;

    public string? Kind { get; } = kind;
}

internal sealed class ArtifactRequestSerializer : NamedPipeSerializer<ArtifactRequest>, INamedPipeSerializer
{
    public override int Id => 5;

    protected override ArtifactRequest DeserializeCore(Stream stream)
    {
        string path = ReadString(stream);
        string? kind = ReadInt(stream) == 1 ? ReadString(stream) : null;
        return new(path, kind);
    }

    protected override void SerializeCore(ArtifactRequest objectToSerialize, Stream stream)
    {
        WriteString(stream, objectToSerialize.Path);
        WriteInt(stream, objectToSerialize.Kind is null ? 0 : 1);
        if (objectToSerialize.Kind is not null)
        {
            WriteString(stream, objectToSerialize.Kind);
        }
    }
}
