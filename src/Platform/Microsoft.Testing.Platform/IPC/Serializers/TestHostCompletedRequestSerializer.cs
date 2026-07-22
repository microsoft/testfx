// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

[Embedded]
internal sealed class TestHostCompletedRequestSerializer : NamedPipeSerializer<TestHostCompletedRequest>, INamedPipeSerializer
{
    public override int Id => TestHostCompletedRequestFieldsId.MessagesSerializerId;

    protected override TestHostCompletedRequest DeserializeCore(Stream stream)
    {
        int exitCode = ReadInt(stream);
        try
        {
            return new TestHostCompletedRequest(exitCode, ReadInt(stream));
        }
        catch (EndOfStreamException)
        {
            // Serializer ID 1 originally carried only ExitCode. Treat a missing second field as a
            // legacy payload whose filtered and unfiltered verdicts are identical.
            return new TestHostCompletedRequest(exitCode);
        }
    }

    protected override void SerializeCore(TestHostCompletedRequest objectToSerialize, Stream stream)
    {
        WriteInt(stream, objectToSerialize.ExitCode);
        WriteInt(stream, objectToSerialize.UnfilteredExitCode);
    }
}
