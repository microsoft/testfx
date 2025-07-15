// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemFileSystem : IFileSystem
{
    public bool ExistFile(string path) => File.Exists(path);

    public string CreateDirectory(string path) => Directory.CreateDirectory(path).FullName;

    public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false) => File.Move(sourceFileName, destFileName, overwrite);

    public IFileStream NewFileStream(string path, FileMode mode) => new SystemFileStream(path, mode);

    public IFileStream NewFileStream(string path, FileMode mode, FileAccess access) => new SystemFileStream(path, mode, access);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

    public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) => File.Copy(sourceFileName, destFileName, overwrite);

    public void DeleteFile(string path) => File.Delete(path);

    public bool ExistDirectory(string? path) => Directory.Exists(path);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => Directory.GetFiles(path, searchPattern, searchOption);
}
