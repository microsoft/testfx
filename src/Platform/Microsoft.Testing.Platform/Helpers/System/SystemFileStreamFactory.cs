// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemFileStreamFactory : IFileStreamFactory
{
    public IFileStream Create(string path, FileMode mode, FileAccess access, FileShare share)
        => new SystemFileStream(path, mode, access, share);
}
