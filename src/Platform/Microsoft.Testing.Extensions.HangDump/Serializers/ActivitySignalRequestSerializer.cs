// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class ActivitySignalRequest : IRequest
{
    public static ActivitySignalRequest Instance { get; } = new();

    private ActivitySignalRequest()
    {
    }
}

internal sealed class ActivitySignalRequestSerializer : NamedPipeSerializer<ActivitySignalRequest>, INamedPipeSerializer
{
    public override int Id => 7;

    protected override ActivitySignalRequest DeserializeCore(Stream stream)
        => ActivitySignalRequest.Instance;

    protected override void SerializeCore(ActivitySignalRequest objectToSerialize, Stream stream)
    {
    }
}
