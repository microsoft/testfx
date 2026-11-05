// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemFileStream : IFileStream
{
    private readonly FileStream _stream;

    public SystemFileStream(FileStream stream, string name)
    {
        _stream = stream;
        Name = name;
    }

    public SystemFileStream(string path, FileMode mode, FileAccess access, FileShare share)
    {
        _stream = new FileStream(path, mode, access, share);
        Name = _stream.Name;
    }

    public SystemFileStream(string path, FileMode mode, FileAccess access)
    {
        _stream = new FileStream(path, mode, access);
        Name = _stream.Name;
    }

    public SystemFileStream(string path, FileMode mode)
    {
        _stream = new FileStream(path, mode);
        Name = _stream.Name;
    }

    public Stream Stream => _stream;

    public string Name { get; }

    public void Dispose()
        => _stream.Dispose();

#if NETCOREAPP
    public ValueTask DisposeAsync()
        => _stream.DisposeAsync();
#endif
}
