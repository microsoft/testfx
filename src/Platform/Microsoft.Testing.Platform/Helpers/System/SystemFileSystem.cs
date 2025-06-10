// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemFileSystem : IFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public string CreateDirectory(string path) => Directory.CreateDirectory(path).FullName;

    public void Move(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);

    public IFileStream NewFileStream(string path, FileMode mode) => new SystemFileStream(path, mode);

    public IFileStream NewFileStream(string path, FileMode mode, FileAccess access) => new SystemFileStream(path, mode, access);

    public string ReadAllText(string path) => File.ReadAllText(path);

#if NETCOREAPP
    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
#else
    public async Task<string> ReadAllTextAsync(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
#endif
}
