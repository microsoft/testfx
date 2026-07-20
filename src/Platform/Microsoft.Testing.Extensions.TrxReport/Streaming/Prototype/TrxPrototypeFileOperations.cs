// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

internal sealed class TrxPrototypeFileOperations : ITrxPrototypeFileOperations
{
#if NETCOREAPP
    public bool SupportsAtomicReplace => true;
#else
    public bool SupportsAtomicReplace => false;
#endif

    public ITrxPrototypeFile Open(string path, FileMode mode, FileAccess access, FileShare share)
        => new TrxPrototypeFile(path, mode, access, share);

    public string CreateTemporarySiblingPath(string destinationPath)
    {
        string fullDestinationPath = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(fullDestinationPath)!;
        string fileName = Path.GetFileName(fullDestinationPath);
        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");
    }

    public void ReplaceTemporarySibling(string temporaryPath, string destinationPath)
#if NETCOREAPP
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporaryPath, destinationPath);
        }
    }
#else
        => throw new PlatformNotSupportedException(
            "Atomic overwrite replacement is unavailable on this runtime. The TRX prototype will not delete the destination before moving a temporary file.");
#endif

    public bool Exists(string path) => File.Exists(path);

    public void Delete(string path) => File.Delete(path);

    private sealed class TrxPrototypeFile : ITrxPrototypeFile
    {
        private readonly FileStream _stream;

        public TrxPrototypeFile(string path, FileMode mode, FileAccess access, FileShare share)
        {
            _stream = new FileStream(path, mode, access, share);
        }

        public long Length => _stream.Length;

        public long Position => _stream.Position;

        public int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

        public void Seek(long offset, SeekOrigin origin) => _ = _stream.Seek(offset, origin);

        public void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

        public void Flush() => _stream.Flush();

        public void SetLength(long length) => _stream.SetLength(length);

        public void Dispose() => _stream.Dispose();
    }
}
