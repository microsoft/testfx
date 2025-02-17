// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is the Process wrapper.")]
internal sealed class SystemProcessHandler : IProcessHandler
{
#pragma warning disable CA1416 // Validate platform compatibility
    public IProcess GetCurrentProcess() => new SystemProcess(Process.GetCurrentProcess());

    public IProcess GetProcessById(int pid) => new SystemProcess(Process.GetProcessById(pid));

    public IProcess Start(ProcessStartInfo startInfo)
    {
        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.CannotStartProcessErrorMessage, startInfo.FileName));
        process.EnableRaisingEvents = true;
        return new SystemProcess(process);
    }
#pragma warning restore CA1416
}
