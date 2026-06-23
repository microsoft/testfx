// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private string BuildDefaultFileName(DateTimeOffset finishTime)
    {
        string user = _environment.GetEnvironmentVariable("UserName")
            ?? _environment.GetEnvironmentVariable("USER")
            ?? "user";
        string moduleName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMoniker();
        string raw = $"{user}_{_environment.MachineName}_{moduleName}_{targetFrameworkMoniker}_{finishTime:yyyy-MM-dd_HH_mm_ss}.ctrf.json";
        return ReplaceInvalidFileNameChars(raw);
    }

    // CTRF `osPlatform` is the short Node-style platform identifier (e.g. "win32",
    // "linux", "darwin"). The full descriptive OS string belongs in `osVersion`.
    private static string GetCtrfOsPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win32";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "darwin";
        }

#if NETCOREAPP
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            return "freebsd";
        }
#endif

        return "unknown";
    }
}
