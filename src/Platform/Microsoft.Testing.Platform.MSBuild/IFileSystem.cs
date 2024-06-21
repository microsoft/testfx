// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.MSBuild;

internal interface IFileSystem
{
    bool Exist(string path);

    void CopyFile(string source, string destination);

    void WriteAllText(string path, string? contents);

    Stream CreateNew(string path);

    void CreateDirectory(string directory);
}
