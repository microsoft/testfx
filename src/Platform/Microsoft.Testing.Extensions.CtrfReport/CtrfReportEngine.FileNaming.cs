// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed partial class CtrfReportEngine
{
    private string BuildDefaultFileName(DateTimeOffset finishTime)
    {
        string user = _environment.GetEnvironmentVariable("UserName")
            ?? _environment.GetEnvironmentVariable("USER")
            ?? "user";
        string moduleName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string targetFrameworkMoniker = GetTargetFrameworkMoniker();
        string raw = $"{user}_{_environment.MachineName}_{moduleName}_{targetFrameworkMoniker}_{finishTime:yyyy-MM-dd_HH_mm_ss}.ctrf.json";
        return ReplaceInvalidFileNameChars(raw);
    }

    private string ResolveJsonFileName(string template)
    {
        string processName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string processId = _environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements(processName, processId, _clock.UtcNow);
        string resolved = ArtifactNamingHelper.ResolveTemplate(template, replacements);
        string directoryPart = Path.GetDirectoryName(resolved) ?? string.Empty;
        string sanitizedFileName = ReplaceInvalidFileNameChars(Path.GetFileName(resolved));
        return directoryPart.Length == 0
            ? sanitizedFileName
            : Path.Combine(directoryPart, sanitizedFileName);
    }

    private static string GetTargetFrameworkMoniker()
        => TargetFrameworkParser.GetShortTargetFramework(
            Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName)
            ?? TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription)
            ?? "unknown";

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

    private static string ReplaceInvalidFileNameChars(string fileName)
    {
        var sb = new StringBuilder(fileName.Length);
        foreach (char c in fileName)
        {
            sb.Append(IsInvalidFileNameChar(c) ? '_' : c);
        }

        string replaced = sb.ToString().TrimEnd();
        if (IsReservedFileName(replaced))
        {
            replaced = '_' + replaced;
        }

        return replaced;
    }

    private static bool IsInvalidFileNameChar(char c)
        // Keep the explicit file-name sanitization aligned with TRX report naming so
        // placeholders and cross-platform reserved characters produce compatible names.
        => c is < ' ' or '"' or '<' or '>' or '|' or ':' or '*' or '?' or '\\' or '/' or '@' or '(' or ')' or '^' or ' ';

    private static bool IsReservedFileName(string fileName)
    {
        string bareName = fileName;
        int dot = bareName.IndexOf('.');
        if (dot >= 0)
        {
            bareName = bareName.Substring(0, dot);
        }

        return bareName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase)
            || IsReservedNameWithNumber(bareName, "COM")
            || IsReservedNameWithNumber(bareName, "LPT");

        static bool IsReservedNameWithNumber(string bareName, string prefix)
            => bareName.Length == 4
                && bareName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && bareName[3] is >= '1' and <= '9';
    }
}
