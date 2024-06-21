// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.MSBuild;

internal sealed class FileSystem : IFileSystem
{
    public void CopyFile(string source, string destination)
    {
        using FileStream fileRead = File.OpenRead(source);
        using FileStream fs = new(destination, FileMode.Create);
        {
            fileRead.CopyTo(fs);
        }
    }

    public void CreateDirectory(string directory) => Directory.CreateDirectory(directory);

    public Stream CreateNew(string path) => new FileStream(path, FileMode.Create);

    public bool Exist(string path) => File.Exists(path);

    public void WriteAllText(string path, string? contents) => File.WriteAllText(path, contents);
}
