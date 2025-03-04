// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is the Process wrapper.")]
internal sealed class SystemProcessHandler : IProcessHandler
{
    public async Task KillAsync(int id)
    {
        using var process = Process.GetProcessById(id);
        process.Kill();
        await process.WaitForExitAsync();
    }

    public int GetCurrentProcessId()
    {
        using var process = Process.GetCurrentProcess();
        return process.Id;
    }

    public (int Id, string Name) GetCurrentProcessInfo()
    {
        using var process = Process.GetCurrentProcess();
        return (process.Id, process.ProcessName);
    }

    public string? GetCurrentProcessFileName()
    {
        using var process = Process.GetCurrentProcess();
        return process.MainModule?.FileName;
    }

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
