// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.HangDump.Serializers;

internal sealed class ExitSignalActivityIndicatorTaskRequest() : IRequest;

internal sealed class ExitSignalActivityIndicatorTaskRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 6;

    public object Deserialize(Stream stream) => new ExitSignalActivityIndicatorTaskRequest();

    public void Serialize(object objectToSerialize, Stream stream)
    {
    }
}
