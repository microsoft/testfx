// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal enum TrxFileOperationKind
{
    Open,
    Read,
    Seek,
    Write,
    Flush,
    SetLength,
    Replace,
    Delete,
}

internal enum TrxReplacementModel
{
    Atomic,
    DeleteThenMove,
    Unsupported,
}

internal sealed class TrxTerminationPlan
{
    public TrxTerminationPlan(int operationIndex, int? committedByteCount = null)
    {
        if (operationIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(operationIndex));
        }

        if (committedByteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(committedByteCount));
        }

        OperationIndex = operationIndex;
        CommittedByteCount = committedByteCount;
    }

    public int OperationIndex { get; }

    public int? CommittedByteCount { get; }
}

internal sealed class TrxFileOperationRecord
{
    public required int OperationIndex { get; init; }

    public required TrxFileOperationKind Kind { get; init; }

    public required string Path { get; init; }

    public long? PrePosition { get; init; }

    public long? PostPosition { get; init; }

    public required long PreLength { get; init; }

    public required long PostLength { get; init; }

    public int RequestedByteCount { get; init; }

    public int CommittedByteCount { get; init; }

    public FileMode? Mode { get; init; }

    public FileAccess? Access { get; init; }

    public FileShare? Share { get; init; }

    public string? ReplacementSource { get; init; }

    public string? ReplacementTarget { get; init; }

    public required string ByteWindow { get; init; }

    public override string ToString()
        => string.Format(
            CultureInfo.InvariantCulture,
            "#{0:D3} {1} path='{2}' position={3}->{4} length={5}->{6} requested={7} committed={8} mode={9} access={10} share={11} replace='{12}'->'{13}' window=[{14}]",
            OperationIndex,
            Kind,
            Path,
            FormatNullable(PrePosition),
            FormatNullable(PostPosition),
            PreLength,
            PostLength,
            RequestedByteCount,
            CommittedByteCount,
            Mode?.ToString() ?? "-",
            Access?.ToString() ?? "-",
            Share?.ToString() ?? "-",
            ReplacementSource ?? "-",
            ReplacementTarget ?? "-",
            ByteWindow);

    private static string FormatNullable(long? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
}

internal sealed class TrxVirtualFileSystemSnapshot
{
    private readonly IReadOnlyDictionary<string, byte[]> _files;

    public TrxVirtualFileSystemSnapshot(
        int operationIndex,
        bool isProcessDead,
        IEnumerable<KeyValuePair<string, byte[]>> files)
    {
        OperationIndex = operationIndex;
        IsProcessDead = isProcessDead;

        var copies = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, byte[]> file in files)
        {
            copies.Add(file.Key, (byte[])file.Value.Clone());
        }

        _files = new ReadOnlyDictionary<string, byte[]>(copies);
    }

    public int OperationIndex { get; }

    public bool IsProcessDead { get; }

    public int FileCount => _files.Count;

    public IReadOnlyList<string> Paths => [.. _files.Keys];

    public bool Contains(string path) => _files.ContainsKey(path);

    public byte[] GetFileBytes(string path) => (byte[])_files[path].Clone();

    public override string ToString()
    {
        StringBuilder builder = new();
        _ = builder.Append("operation=")
            .Append(OperationIndex.ToString(CultureInfo.InvariantCulture))
            .Append("; dead=")
            .Append(IsProcessDead)
            .Append("; files=");
        foreach (KeyValuePair<string, byte[]> file in _files)
        {
            _ = builder.Append('[')
                .Append(file.Key)
                .Append(':')
                .Append(Convert.ToBase64String(file.Value))
                .Append(']');
        }

        return builder.ToString();
    }
}

internal sealed class TrxSimulatedProcessTerminationException : IOException
{
    public TrxSimulatedProcessTerminationException(int operationIndex)
        : base($"The virtual TRX process terminated at semantic file operation {operationIndex}.")
    {
        OperationIndex = operationIndex;
    }

    public int OperationIndex { get; }
}

internal sealed class TrxFaultInjectingFileOperations : ITrxPrototypeFileOperations
{
    private const int DiagnosticWindowSize = 16;

#pragma warning disable IDE0028 // Collection expressions cannot preserve the ordinal path comparer.
    private readonly Dictionary<string, VirtualFileEntry> _files = new(StringComparer.Ordinal);
#pragma warning restore IDE0028
    private readonly List<VirtualFileHandle> _handles = [];
    private readonly List<TrxFileOperationRecord> _operations = [];
    private readonly List<TrxVirtualFileSystemSnapshot> _snapshots = [];
    private readonly TrxReplacementModel _replacementModel;
    private readonly Action<TrxFileOperationRecord, TrxVirtualFileSystemSnapshot>? _afterOperation;
    private readonly bool _enforceShareSemantics;
    private readonly bool _captureSnapshots;
    private TrxTerminationPlan? _terminationPlan;
    private bool _recordOperations;
    private int _nextOperationIndex;
    private int _nextTemporaryPath;

    public TrxFaultInjectingFileOperations(
        TrxReplacementModel replacementModel = TrxReplacementModel.Atomic,
        TrxTerminationPlan? terminationPlan = null,
        Action<TrxFileOperationRecord, TrxVirtualFileSystemSnapshot>? afterOperation = null,
        bool enforceShareSemantics = true,
        bool captureSnapshots = true,
        bool recordOperations = true)
    {
        _replacementModel = replacementModel;
        _terminationPlan = terminationPlan;
        _afterOperation = afterOperation;
        _enforceShareSemantics = enforceShareSemantics;
        _captureSnapshots = captureSnapshots;
        _recordOperations = recordOperations;
    }

    public bool SupportsAtomicReplace => _replacementModel == TrxReplacementModel.Atomic;

    public bool IsProcessDead { get; private set; }

    public IReadOnlyList<TrxFileOperationRecord> Operations => [.. _operations];

    public IReadOnlyList<TrxVirtualFileSystemSnapshot> Snapshots => [.. _snapshots];

    public void SeedFile(string path, byte[] bytes)
    {
        if (_nextOperationIndex != 0 || _handles.Count != 0)
        {
            throw new InvalidOperationException("Virtual files can only be seeded before semantic operations begin.");
        }

        _files[path] = new VirtualFileEntry(bytes);
    }

    public byte[] GetFileBytes(string path) => (byte[])_files[path].Bytes.Clone();

    public TrxVirtualFileSystemSnapshot CaptureSnapshot()
        => CreateSnapshot(_nextOperationIndex - 1);

    public string FormatTrace() => string.Join(Environment.NewLine, _operations);

    public void BeginFaultWindow(TrxTerminationPlan? terminationPlan)
    {
        EnsureProcessAlive();
        if (_handles.Count != 0)
        {
            throw new InvalidOperationException("A fault window can only begin after every virtual file handle is closed.");
        }

        _operations.Clear();
        _snapshots.Clear();
        _nextOperationIndex = 0;
        _terminationPlan = terminationPlan;
        _recordOperations = true;
    }

    public ITrxPrototypeFile Open(string path, FileMode mode, FileAccess access, FileShare share)
    {
        EnsureProcessAlive();
        ValidateOpenArguments(mode, access);

        _files.TryGetValue(path, out VirtualFileEntry? entry);
        if (mode == FileMode.CreateNew && entry is not null)
        {
            throw new IOException($"The virtual file '{path}' already exists.");
        }

        if ((mode == FileMode.Open || mode == FileMode.Truncate) && entry is null)
        {
            throw new FileNotFoundException($"The virtual file '{path}' does not exist.", path);
        }

        long preLength = entry?.Bytes.LongLength ?? 0;
        if (entry is not null)
        {
            EnsureOpenShareIsCompatible(entry, access, share);
        }

        if (entry is null)
        {
            entry = new VirtualFileEntry([]);
            _files.Add(path, entry);
        }

        if (mode is FileMode.Create or FileMode.Truncate)
        {
            entry.SetBytes([]);
        }

        long position = mode == FileMode.Append ? entry.Bytes.LongLength : 0;
        var handle = new VirtualFileHandle(this, path, entry, access, share, position);
        _handles.Add(handle);

        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Open,
            Path = path,
            PreLength = preLength,
            PostLength = entry.Bytes.LongLength,
            PostPosition = position,
            Mode = mode,
            Access = access,
            Share = share,
            ByteWindow = CreateByteWindow(entry.Bytes, position),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
        return handle;
    }

    public string CreateTemporarySiblingPath(string destinationPath)
    {
        EnsureProcessAlive();
        string? directory = Path.GetDirectoryName(destinationPath);
        string fileName = Path.GetFileName(destinationPath);
        string candidate;
        do
        {
            _nextTemporaryPath++;
            candidate = Path.Combine(
                directory ?? string.Empty,
                $"{fileName}.prototype-{_nextTemporaryPath:D4}.tmp");
        }
        while (_files.ContainsKey(candidate));

        return candidate;
    }

    public void ReplaceTemporarySibling(string temporaryPath, string destinationPath)
    {
        EnsureProcessAlive();
        if (_replacementModel == TrxReplacementModel.Unsupported)
        {
            throw new PlatformNotSupportedException("Atomic replacement is disabled in this virtual filesystem.");
        }

        if (_replacementModel == TrxReplacementModel.DeleteThenMove)
        {
            DeleteCore(destinationPath);
            MoveTemporaryEntry(temporaryPath, destinationPath);
            return;
        }

        AtomicReplace(temporaryPath, destinationPath);
    }

    public bool Exists(string path)
    {
        EnsureProcessAlive();
        return _files.ContainsKey(path);
    }

    public void Delete(string path)
    {
        EnsureProcessAlive();
        DeleteCore(path);
    }

    private void AtomicReplace(string temporaryPath, string destinationPath)
    {
        VirtualFileEntry source = GetRequiredEntry(temporaryPath);
        _files.TryGetValue(destinationPath, out VirtualFileEntry? destination);
        EnsureDeleteShareIsCompatible(source, temporaryPath);
        if (destination is not null && !ReferenceEquals(source, destination))
        {
            EnsureDeleteShareIsCompatible(destination, destinationPath);
        }

        long preLength = destination?.Bytes.LongLength ?? 0;
        _ = _files.Remove(temporaryPath);
        _files[destinationPath] = source;

        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Replace,
            Path = destinationPath,
            PreLength = preLength,
            PostLength = source.Bytes.LongLength,
            ReplacementSource = temporaryPath,
            ReplacementTarget = destinationPath,
            ByteWindow = CreateByteWindow(source.Bytes, source.Bytes.LongLength),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
    }

    private void MoveTemporaryEntry(string temporaryPath, string destinationPath)
    {
        VirtualFileEntry source = GetRequiredEntry(temporaryPath);
        EnsureDeleteShareIsCompatible(source, temporaryPath);

        long preLength = _files.TryGetValue(destinationPath, out VirtualFileEntry? destination)
            ? destination.Bytes.LongLength
            : 0;
        if (destination is not null)
        {
            EnsureDeleteShareIsCompatible(destination, destinationPath);
        }

        _ = _files.Remove(temporaryPath);
        _files[destinationPath] = source;

        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Replace,
            Path = destinationPath,
            PreLength = preLength,
            PostLength = source.Bytes.LongLength,
            ReplacementSource = temporaryPath,
            ReplacementTarget = destinationPath,
            ByteWindow = CreateByteWindow(source.Bytes, source.Bytes.LongLength),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
    }

    private void DeleteCore(string path)
    {
        _files.TryGetValue(path, out VirtualFileEntry? entry);
        long preLength = entry?.Bytes.LongLength ?? 0;
        if (entry is not null)
        {
            EnsureDeleteShareIsCompatible(entry, path);
            _ = _files.Remove(path);
        }

        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Delete,
            Path = path,
            PreLength = preLength,
            PostLength = 0,
            ByteWindow = CreateByteWindow([], 0),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
    }

    private int Read(VirtualFileHandle handle, byte[] buffer, int offset, int count)
    {
        EnsureHandleUsable(handle);
        EnsureReadable(handle);
        ValidateBufferArguments(buffer, offset, count);

        long prePosition = handle.Position;
        long preLength = handle.Entry.Bytes.LongLength;
        int available = (int)Math.Min(count, Math.Max(0, preLength - prePosition));
        if (available > 0)
        {
            Array.Copy(handle.Entry.Bytes, prePosition, buffer, offset, available);
            handle.Position += available;
        }

        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Read,
            Path = handle.Path,
            PrePosition = prePosition,
            PostPosition = handle.Position,
            PreLength = preLength,
            PostLength = handle.Entry.Bytes.LongLength,
            RequestedByteCount = count,
            CommittedByteCount = available,
            ByteWindow = CreateByteWindow(handle.Entry.Bytes, handle.Position),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
        return available;
    }

    private void Seek(VirtualFileHandle handle, long offset, SeekOrigin origin)
    {
        EnsureHandleUsable(handle);
        long prePosition = handle.Position;
        long preLength = handle.Entry.Bytes.LongLength;
        long originPosition = origin switch
        {
            SeekOrigin.Begin => 0,
            SeekOrigin.Current => handle.Position,
            SeekOrigin.End => preLength,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        long postPosition = checked(originPosition + offset);
        if (postPosition < 0)
        {
            throw new IOException("An attempt was made to seek before the beginning of the virtual file.");
        }

        handle.Position = postPosition;
        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Seek,
            Path = handle.Path,
            PrePosition = prePosition,
            PostPosition = postPosition,
            PreLength = preLength,
            PostLength = preLength,
            ByteWindow = CreateByteWindow(handle.Entry.Bytes, postPosition),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
    }

    private void Write(VirtualFileHandle handle, byte[] buffer, int offset, int count)
    {
        EnsureHandleUsable(handle);
        EnsureWritable(handle);
        ValidateBufferArguments(buffer, offset, count);

        int operationIndex = _nextOperationIndex;
        int committedByteCount = GetCommittedWriteCount(operationIndex, count);
        long prePosition = handle.Position;
        long preLength = handle.Entry.Bytes.LongLength;
        long requiredLength = checked(prePosition + committedByteCount);
        if (requiredLength > int.MaxValue)
        {
            throw new IOException("The virtual file cannot exceed Int32.MaxValue bytes.");
        }

        if (requiredLength > preLength)
        {
            handle.Entry.Resize((int)requiredLength);
        }

        if (committedByteCount > 0)
        {
            Array.Copy(buffer, offset, handle.Entry.Bytes, prePosition, committedByteCount);
            handle.Position += committedByteCount;
        }

        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Write,
            Path = handle.Path,
            PrePosition = prePosition,
            PostPosition = handle.Position,
            PreLength = preLength,
            PostLength = handle.Entry.Bytes.LongLength,
            RequestedByteCount = count,
            CommittedByteCount = committedByteCount,
            ByteWindow = CreateByteWindow(handle.Entry.Bytes, handle.Position),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
    }

    private void Flush(VirtualFileHandle handle)
    {
        EnsureHandleUsable(handle);
        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.Flush,
            Path = handle.Path,
            PrePosition = handle.Position,
            PostPosition = handle.Position,
            PreLength = handle.Entry.Bytes.LongLength,
            PostLength = handle.Entry.Bytes.LongLength,
            ByteWindow = CreateByteWindow(handle.Entry.Bytes, handle.Position),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
    }

    private void SetLength(VirtualFileHandle handle, long length)
    {
        EnsureHandleUsable(handle);
        EnsureWritable(handle);
        if (length is < 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        long preLength = handle.Entry.Bytes.LongLength;
        handle.Entry.Resize((int)length);
        int operationIndex = _nextOperationIndex;
        var record = new TrxFileOperationRecord
        {
            OperationIndex = operationIndex,
            Kind = TrxFileOperationKind.SetLength,
            Path = handle.Path,
            PrePosition = handle.Position,
            PostPosition = handle.Position,
            PreLength = preLength,
            PostLength = length,
            ByteWindow = CreateByteWindow(handle.Entry.Bytes, handle.Position),
        };
        RecordAndFinish(record, ShouldTerminate(operationIndex));
    }

    private long GetLength(VirtualFileHandle handle)
    {
        EnsureHandleUsable(handle);
        return handle.Entry.Bytes.LongLength;
    }

    private long GetPosition(VirtualFileHandle handle)
    {
        EnsureHandleUsable(handle);
        return handle.Position;
    }

    private void DisposeHandle(VirtualFileHandle handle)
    {
        EnsureProcessAlive();
        if (handle.IsDisposed)
        {
            return;
        }

        handle.IsDisposed = true;
        _ = _handles.Remove(handle);
    }

    private int GetCommittedWriteCount(int operationIndex, int requestedByteCount)
    {
        int committedByteCount = _terminationPlan?.OperationIndex == operationIndex
            && _terminationPlan.CommittedByteCount is { } plannedByteCount
                ? plannedByteCount
                : requestedByteCount;

        return committedByteCount <= requestedByteCount
            ? committedByteCount
            : throw new InvalidOperationException(
                $"The termination plan requests {committedByteCount} committed bytes for a {requestedByteCount}-byte write.");
    }

    private bool ShouldTerminate(int operationIndex)
        => _terminationPlan?.OperationIndex == operationIndex;

    private void RecordAndFinish(TrxFileOperationRecord record, bool terminate)
    {
        if (terminate)
        {
            IsProcessDead = true;
        }

        if (_recordOperations)
        {
            _operations.Add(record);
        }

        _nextOperationIndex++;
        if (_recordOperations && (_captureSnapshots || _afterOperation is not null))
        {
            TrxVirtualFileSystemSnapshot snapshot = CreateSnapshot(record.OperationIndex);
            if (_captureSnapshots)
            {
                _snapshots.Add(snapshot);
            }

            _afterOperation?.Invoke(record, snapshot);
        }

        if (terminate)
        {
            throw new TrxSimulatedProcessTerminationException(record.OperationIndex);
        }
    }

    private TrxVirtualFileSystemSnapshot CreateSnapshot(int operationIndex)
        => new(
            operationIndex,
            IsProcessDead,
            _files.Select(file => new KeyValuePair<string, byte[]>(file.Key, file.Value.Bytes)));

    private void EnsureProcessAlive()
    {
#pragma warning disable IDE0046 // Keep the dead-process guard explicit at this operation boundary.
        if (IsProcessDead)
        {
            int operationIndex = _terminationPlan?.OperationIndex ?? _nextOperationIndex - 1;
            throw new TrxSimulatedProcessTerminationException(operationIndex);
        }
#pragma warning restore IDE0046
    }

    private void EnsureHandleUsable(VirtualFileHandle handle)
    {
        EnsureProcessAlive();
        if (handle.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ITrxPrototypeFile));
        }
    }

    private static void EnsureReadable(VirtualFileHandle handle)
    {
        if ((handle.Access & FileAccess.Read) == 0)
        {
            throw new NotSupportedException("The virtual handle does not permit reading.");
        }
    }

    private static void EnsureWritable(VirtualFileHandle handle)
    {
        if ((handle.Access & FileAccess.Write) == 0)
        {
            throw new NotSupportedException("The virtual handle does not permit writing.");
        }
    }

    private void EnsureOpenShareIsCompatible(VirtualFileEntry entry, FileAccess access, FileShare share)
    {
        if (!_enforceShareSemantics)
        {
            return;
        }

        foreach (VirtualFileHandle existing in _handles.Where(handle => !handle.IsDisposed && ReferenceEquals(handle.Entry, entry)))
        {
            if (!ShareAllowsAccess(existing.Share, access) || !ShareAllowsAccess(share, existing.Access))
            {
                throw new IOException("The requested virtual file access conflicts with an existing handle's share mode.");
            }
        }
    }

    private void EnsureDeleteShareIsCompatible(VirtualFileEntry entry, string path)
    {
        if (!_enforceShareSemantics)
        {
            return;
        }

        if (_handles.Any(handle =>
                !handle.IsDisposed
                && ReferenceEquals(handle.Entry, entry)
                && (handle.Share & FileShare.Delete) == 0))
        {
            throw new IOException($"The virtual file '{path}' is open without FileShare.Delete.");
        }
    }

    private static bool ShareAllowsAccess(FileShare share, FileAccess access)
        => ((access & FileAccess.Read) == 0 || (share & FileShare.Read) != 0)
            && ((access & FileAccess.Write) == 0 || (share & FileShare.Write) != 0);

    private static void ValidateOpenArguments(FileMode mode, FileAccess access)
    {
        if (mode == FileMode.Append && access != FileAccess.Write)
        {
            throw new ArgumentException("Append mode requires write-only access.", nameof(access));
        }

        if (mode is FileMode.Create or FileMode.CreateNew or FileMode.Truncate
            && (access & FileAccess.Write) == 0)
        {
            throw new ArgumentException($"{mode} mode requires write access.", nameof(access));
        }
    }

    private static void ValidateBufferArguments(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || count < 0 || offset > buffer.Length - count)
        {
            throw new ArgumentOutOfRangeException(
                offset < 0 ? nameof(offset) : nameof(count),
                "The offset and count must select a valid buffer range.");
        }
    }

    private VirtualFileEntry GetRequiredEntry(string path)
        => _files.TryGetValue(path, out VirtualFileEntry? entry)
            ? entry
            : throw new FileNotFoundException($"The virtual file '{path}' does not exist.", path);

    private static string CreateByteWindow(byte[] bytes, long position)
    {
        int center = (int)Math.Min(Math.Max(position, 0), bytes.LongLength);
        int start = Math.Max(0, center - (DiagnosticWindowSize / 2));
        if (start + DiagnosticWindowSize > bytes.Length)
        {
            start = Math.Max(0, bytes.Length - DiagnosticWindowSize);
        }

        int count = Math.Min(DiagnosticWindowSize, bytes.Length - start);
        byte[] window = new byte[count];
        Array.Copy(bytes, start, window, 0, count);
        string hex = BitConverter.ToString(window).Replace("-", " ");
        string utf8 = Encoding.UTF8.GetString(window);
        StringBuilder text = new(utf8.Length);
        foreach (char value in utf8)
        {
            text.Append(char.IsControl(value) ? '.' : value);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "offset={0}; count={1}; hex={2}; utf8={3}",
            start,
            count,
            hex,
            text);
    }

    private sealed class VirtualFileEntry
    {
        private byte[] _bytes;

        public VirtualFileEntry(byte[] bytes)
        {
            _bytes = (byte[])bytes.Clone();
        }

        public byte[] Bytes => _bytes;

        public void Resize(int length) => Array.Resize(ref _bytes, length);

        public void SetBytes(byte[] bytes) => _bytes = bytes;
    }

    private sealed class VirtualFileHandle : ITrxPrototypeFile
    {
        private readonly TrxFaultInjectingFileOperations _owner;

        public VirtualFileHandle(
            TrxFaultInjectingFileOperations owner,
            string path,
            VirtualFileEntry entry,
            FileAccess access,
            FileShare share,
            long position)
        {
            _owner = owner;
            Path = path;
            Entry = entry;
            Access = access;
            Share = share;
            Position = position;
        }

        public string Path { get; }

        public VirtualFileEntry Entry { get; }

        public FileAccess Access { get; }

        public FileShare Share { get; }

        public long Position { get; set; }

        public bool IsDisposed { get; set; }

        long ITrxPrototypeFile.Length => _owner.GetLength(this);

        long ITrxPrototypeFile.Position => _owner.GetPosition(this);

        int ITrxPrototypeFile.Read(byte[] buffer, int offset, int count)
            => _owner.Read(this, buffer, offset, count);

        void ITrxPrototypeFile.Seek(long offset, SeekOrigin origin) => _owner.Seek(this, offset, origin);

        void ITrxPrototypeFile.Write(byte[] buffer, int offset, int count)
            => _owner.Write(this, buffer, offset, count);

        void ITrxPrototypeFile.Flush() => _owner.Flush(this);

        void ITrxPrototypeFile.SetLength(long length) => _owner.SetLength(this, length);

        public void Dispose() => _owner.DisposeHandle(this);
    }
}
