// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IFileSystem
{
    bool ExistFile(string path);

    bool ExistDirectory(string? path);

    string CreateDirectory(string path);

    void MoveFile(string sourceFileName, string destFileName, bool overwrite = false);

    IFileStream NewFileStream(string path, FileMode mode);

    IFileStream NewFileStream(string path, FileMode mode, FileAccess access);

    string ReadAllText(string path);

    Task<string> ReadAllTextAsync(string path);

    void CopyFile(string sourceFileName, string destFileName, bool overwrite = false);

    void DeleteFile(string path);

    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
}
