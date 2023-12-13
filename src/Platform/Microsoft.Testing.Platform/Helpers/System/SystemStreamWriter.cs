// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemStreamWriter : IStreamWriter
{
    private readonly StreamWriter _writer;

    public SystemStreamWriter(IFileStream stream, Encoding encoding)
    {
        _writer = new StreamWriter(stream.Stream, encoding);
    }

    public bool AutoFlush
    {
        get { return _writer.AutoFlush; }
        set { _writer.AutoFlush = value; }
    }

    public void Dispose()
        => _writer.Dispose();

#if NETCOREAPP
    public ValueTask DisposeAsync()
        => _writer.DisposeAsync();
#endif

    public void Flush() => _writer.Flush();

    public Task FlushAsync() => _writer.FlushAsync();

    public void WriteLine(string? item) => _writer.WriteLine(item);

    public Task WriteLineAsync(string? item) => _writer.WriteLineAsync(item);
}
