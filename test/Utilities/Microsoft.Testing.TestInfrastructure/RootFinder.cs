// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public static class RootFinder
{
    public static string Find()
    {
        string path = AppContext.BaseDirectory;
        string dir = path;
        while (Directory.GetDirectoryRoot(dir) != dir)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            else
            {
                dir = Directory.GetParent(dir)!.ToString();
            }
        }

        throw new InvalidOperationException($"Could not find solution root, .git not found in {path} or any parent directory.");
    }
}
