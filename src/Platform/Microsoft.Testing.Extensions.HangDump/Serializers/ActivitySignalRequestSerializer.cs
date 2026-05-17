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

internal sealed class ActivitySignalRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 7;

    public object Deserialize(Stream stream)
        => ActivitySignalRequest.Instance;

    public void Serialize(object objectToSerialize, Stream stream)
    {
    }
}
