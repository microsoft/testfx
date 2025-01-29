// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.MSBuild.Serializers;

internal sealed record ModuleInfoRequest(string FrameworkDescription, string ProcessArchitecture, string TestResultFolder) : IRequest;

internal sealed class ModuleInfoRequestSerializer : BaseSerializer, INamedPipeSerializer
{
    public int Id => 1;

    public object Deserialize(Stream stream)
        => new ModuleInfoRequest(ReadString(stream), ReadString(stream), ReadString(stream));

    public void Serialize(object objectToSerialize, Stream stream)
    {
        var moduleInfo = (ModuleInfoRequest)objectToSerialize;
        WriteString(stream, moduleInfo.FrameworkDescription);
        WriteString(stream, moduleInfo.ProcessArchitecture);
        WriteString(stream, moduleInfo.TestResultFolder);
    }
}
