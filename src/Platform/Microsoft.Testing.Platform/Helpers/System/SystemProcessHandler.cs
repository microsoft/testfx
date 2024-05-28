// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is the Process wrapper.")]
internal sealed class SystemProcessHandler : IProcessHandler
{
    public IProcess GetCurrentProcess() => new SystemProcess(Process.GetCurrentProcess());

    public IProcess GetProcessById(int pid) => new SystemProcess(Process.GetProcessById(pid));

    public IProcess Start(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.CannotStartProcessErrorMessage, processStartInfo.FileName));
        process.EnableRaisingEvents = true;
        return new SystemProcess(process);
    }
}
