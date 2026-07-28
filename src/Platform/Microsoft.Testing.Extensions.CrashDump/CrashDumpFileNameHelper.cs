// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed partial class CrashDumpProcessLifetimeHandler
{
    private static class CrashDumpFileNameHelper
    {
        public static string GetDumpDirectory(string dumpFileNamePattern)
        {
            // Path.GetDirectoryName returns "" for a bare filename on modern .NET and throws for an
            // empty string on .NET Framework. Both cases mean the current working directory.
            if (dumpFileNamePattern is null or "")
            {
                return ".";
            }

            string? rawDirectory = Path.GetDirectoryName(dumpFileNamePattern);
            return rawDirectory is null or "" ? "." : rawDirectory;
        }

        public static string GetDumpSearchPattern(string dumpFileNamePattern)
        {
            string dumpExtension = Path.GetExtension(Path.GetFileName(dumpFileNamePattern));
            return dumpExtension.Length == 0 ? "*" : $"*{dumpExtension}";
        }

        public static Regex BuildDumpFileNameRegex(string fileName)
            => new(BuildDumpFileNameRegexPattern(fileName), RegexOptions.CultureInvariant);

        public static string BuildDumpFileNameRegexPattern(string fileName)
        {
            var sb = new StringBuilder("^");
            bool lastWasWildcard = false;
            for (int i = 0; i < fileName.Length; i++)
            {
                if (fileName[i] == '%' && i + 1 < fileName.Length)
                {
                    if (fileName[i + 1] == '%')
                    {
                        sb.Append('%');
                        lastWasWildcard = false;
                        i++;
                        continue;
                    }

                    if (!lastWasWildcard)
                    {
                        sb.Append(".*");
                        lastWasWildcard = true;
                    }

                    i++;
                }
                else
                {
                    sb.Append(Regex.Escape(fileName[i].ToString()));
                    lastWasWildcard = false;
                }
            }

            sb.Append('$');
            return sb.ToString();
        }
    }
}
