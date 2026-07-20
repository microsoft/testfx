// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

internal interface ITrxPrototypeFileOperations
{
    bool SupportsAtomicReplace { get; }

    ITrxPrototypeFile Open(string path, FileMode mode, FileAccess access, FileShare share);

    string CreateTemporarySiblingPath(string destinationPath);

    void ReplaceTemporarySibling(string temporaryPath, string destinationPath);

    bool Exists(string path);

    void Delete(string path);
}

internal interface ITrxPrototypeFile : IDisposable
{
    long Length { get; }

    long Position { get; }

    int Read(byte[] buffer, int offset, int count);

    void Seek(long offset, SeekOrigin origin);

    void Write(byte[] buffer, int offset, int count);

    void Flush();

    void SetLength(long length);
}
