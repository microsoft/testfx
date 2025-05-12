// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal static class RootFinder
{
    private static string? s_root;

    public static string Find()
    {
        if (s_root != null)
        {
            return s_root;
        }

        string path = AppContext.BaseDirectory;
        string dir = path;
        while (Directory.GetDirectoryRoot(dir) != dir)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                s_root = dir + Path.DirectorySeparatorChar;
                return dir + Path.DirectorySeparatorChar;
            }
            else
            {
                dir = Directory.GetParent(dir)!.ToString();
            }
        }

        throw new InvalidOperationException($"Could not find solution root, .git not found in {path} or any parent directory.");
    }
}
