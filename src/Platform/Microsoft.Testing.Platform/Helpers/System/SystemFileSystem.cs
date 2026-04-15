// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemFileSystem : IFileSystem
{
    public bool ExistFile(string path) => File.Exists(path);

    public string CreateDirectory(string path) => Directory.CreateDirectory(path).FullName;

#if NETCOREAPP
    public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false) => File.Move(sourceFileName, destFileName, overwrite);
#else
    public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false)
    {
        if (overwrite)
        {
            File.Copy(sourceFileName, destFileName, overwrite: true);
            File.Delete(sourceFileName);
        }
        else
        {
            File.Move(sourceFileName, destFileName);
        }
    }
#endif

    public IFileStream NewFileStream(string path, FileMode mode) => new SystemFileStream(path, mode);

    public IFileStream NewFileStream(string path, FileMode mode, FileAccess access) => new SystemFileStream(path, mode, access);

    public string ReadAllText(string path) => File.ReadAllText(path);

#if NETCOREAPP
    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
#else
    public Task<string> ReadAllTextAsync(string path)
    {
        using StreamReader reader = AsyncStreamReader(path, Encoding.UTF8);
        return reader.ReadToEndAsync();

        static StreamReader AsyncStreamReader(string path, Encoding encoding)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new(stream, encoding, detectEncodingFromByteOrderMarks: true);
        }
    }
#endif

    public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) => File.Copy(sourceFileName, destFileName, overwrite);

    public void DeleteFile(string path) => File.Delete(path);

    public bool ExistDirectory(string? path) => Directory.Exists(path);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);
}
