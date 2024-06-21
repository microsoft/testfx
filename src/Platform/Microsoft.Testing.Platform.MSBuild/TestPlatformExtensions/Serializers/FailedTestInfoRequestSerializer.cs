// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform.MSBuild.TestPlatformExtensions.Serializers;

internal record FailedTestInfoRequest(
    string DisplayName,
    bool IsCancelled,
    string? Duration,
    string? ErrorMessage,
    string? ErrorStackTrace,
    string? Expected,
    string? Actual,
    string? CodeFilePath,
    int LineNumber) : IRequest;

internal class FailedTestInfoRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 2;

    public object Deserialize(Stream stream)
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

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var failedTestInfoRequest = (FailedTestInfoRequest)objectToSerialize;
        WriteString(stream, failedTestInfoRequest.DisplayName);
        WriteInt(stream, failedTestInfoRequest.IsCancelled ? 1 : 0);
        WriteString(stream, failedTestInfoRequest.Duration ?? string.Empty);
        WriteString(stream, failedTestInfoRequest.ErrorMessage ?? string.Empty);
        WriteString(stream, failedTestInfoRequest.ErrorStackTrace ?? string.Empty);
        WriteString(stream, failedTestInfoRequest.Expected ?? string.Empty);
        WriteString(stream, failedTestInfoRequest.Actual ?? string.Empty);
        WriteString(stream, failedTestInfoRequest.CodeFilePath ?? string.Empty);
        WriteInt(stream, failedTestInfoRequest.LineNumber);
    }
}
