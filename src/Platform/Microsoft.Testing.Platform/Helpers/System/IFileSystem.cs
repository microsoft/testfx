﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IFileSystem
{
    bool Exists(string path);

    string CreateDirectory(string path);

    void Move(string sourceFileName, string destFileName);

    IFileStream NewFileStream(string path, FileMode mode);

    IFileStream NewFileStream(string path, FileMode mode, FileAccess access);

    string ReadAllText(string path);

    Task<string> ReadAllTextAsync(string path);
}
