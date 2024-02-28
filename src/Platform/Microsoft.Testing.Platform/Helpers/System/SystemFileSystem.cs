// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemFileSystem : IFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public string CreateDirectory(string path) => Directory.CreateDirectory(path).FullName;

    public void Move(string sourceFileName, string destFileName) => File.Move(sourceFileName, destFileName);

    public Stream NewFileStream(string path, FileMode mode) => new FileStream(path, mode);

    public Stream NewFileStream(string path, FileMode mode, FileAccess access) => new FileStream(path, mode, access);

    public string ReadAllText(string path) => File.ReadAllText(path);
}
