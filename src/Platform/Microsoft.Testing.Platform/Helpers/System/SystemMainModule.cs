// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Platform.Helpers;

#if NETCOREAPP
internal sealed class SystemMainModule(ProcessModule? processModule) : IMainModule
{
    private readonly ProcessModule? _processModule = processModule;

    public string? FileName => _processModule?.FileName;
}
#else
internal sealed class SystemMainModule : IMainModule
{
    private readonly ProcessModule _processModule;

    public SystemMainModule(ProcessModule processModule) => _processModule = processModule;

    public string FileName => _processModule.FileName;
}
#endif

