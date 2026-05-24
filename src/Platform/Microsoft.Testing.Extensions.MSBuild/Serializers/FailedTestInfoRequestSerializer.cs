// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

internal sealed record FailedTestInfoRequest(
    string DisplayName,
    bool IsCanceled,
    string? Duration,
    string? ErrorMessage,
    string? ErrorStackTrace,
    string? Expected,
    string? Actual,
    string? CodeFilePath,
    int LineNumber) : IRequest;

internal sealed class FailedTestInfoRequestSerializer : NamedPipeSerializer<FailedTestInfoRequest>, INamedPipeSerializer
{
    public override int Id => 2;

    protected override FailedTestInfoRequest DeserializeCore(Stream stream)
        => new FailedTestInfoRequest(
            ReadString(stream),
            ReadInt(stream) == 1,
            ReadString(stream),
            ReadString(stream),
            ReadString(stream),
            ReadString(stream),
            ReadString(stream),
            ReadString(stream),
            ReadInt(stream));

    protected override void SerializeCore(FailedTestInfoRequest objectToSerialize, Stream stream)
    {
        WriteString(stream, objectToSerialize.DisplayName);
        WriteInt(stream, objectToSerialize.IsCanceled ? 1 : 0);
        WriteString(stream, objectToSerialize.Duration ?? string.Empty);
        WriteString(stream, objectToSerialize.ErrorMessage ?? string.Empty);
        WriteString(stream, objectToSerialize.ErrorStackTrace ?? string.Empty);
        WriteString(stream, objectToSerialize.Expected ?? string.Empty);
        WriteString(stream, objectToSerialize.Actual ?? string.Empty);
        WriteString(stream, objectToSerialize.CodeFilePath ?? string.Empty);
        WriteInt(stream, objectToSerialize.LineNumber);
    }
}
