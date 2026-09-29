// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.PackagedApp;

internal static class PackagedAppScratchArtifactRecovery
{
    private static readonly EnumerationOptions ReparsePointSafeEnumerationOptions = new()
    {
        AttributesToSkip = FileAttributes.ReparsePoint,
        RecurseSubdirectories = true,
    };

    public static void Recover(string scratchDirectory, string recoveryDirectory)
    {
        Directory.CreateDirectory(recoveryDirectory);
        foreach (string sourcePath in Directory.EnumerateFiles(scratchDirectory, "*", ReparsePointSafeEnumerationOptions))
        {
            if ((File.GetAttributes(sourcePath) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            string relativePath = Path.GetRelativePath(scratchDirectory, sourcePath);
            string destinationPath = Path.Combine(recoveryDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }
}
