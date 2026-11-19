// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed partial class SystemFileSystem : IFileSystem
{
    public bool ExistFile(string path) => File.Exists(path);

    public bool IsRegularFile(string path)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.Device)) == 0
            : IsRegularUnixFile(path);

    public IFileStream? OpenReadRegularFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OpenReadRegularUnixFile(fullPath);
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        bool transferred = false;
        try
        {
            if (!stream.CanSeek)
            {
                return null;
            }

            var result = new SystemFileStream(stream, fullPath);
            transferred = true;
            return result;
        }
        finally
        {
            if (!transferred)
            {
                stream.Dispose();
            }
        }
    }

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

    public void ReplaceFile(string sourceFileName, string destFileName)
    {
        if (File.Exists(destFileName))
        {
            File.Replace(sourceFileName, destFileName, destinationBackupFileName: null);
        }
        else
        {
            File.Move(sourceFileName, destFileName);
        }
    }

    public IFileStream NewFileStream(string path, FileMode mode) => new SystemFileStream(path, mode);

    public IFileStream NewFileStream(string path, FileMode mode, FileAccess access) => new SystemFileStream(path, mode, access);

    public IFileStream NewFileStream(string path, FileMode mode, FileAccess access, FileShare share) => new SystemFileStream(path, mode, access, share);

    public string ReadAllText(string path) => File.ReadAllText(path);

#if NETCOREAPP
    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);
#else
    public async Task<string> ReadAllTextAsync(string path)
    {
        using StreamReader reader = AsyncStreamReader(path, Encoding.UTF8);
        return await reader.ReadToEndAsync().ConfigureAwait(false);

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
