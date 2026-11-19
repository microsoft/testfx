// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Recovery infrastructure is shared-source implementation detail, not package API.

internal enum BoundedLineReadResult
{
    Line,
    End,
    LimitExceeded,
}

internal sealed class BoundedUtf8LineReader
{
    private const int BufferSize = 8192;

    private readonly Stream _stream;
    private readonly long _maxBytes;
    private readonly int _maxLineBytes;
    private readonly int _maxLineChars;
    private readonly byte[] _readBuffer = new byte[BufferSize];
    private readonly byte[] _lineBuffer;
    private int _readOffset;
    private int _readCount;
    private long _bytesRead;

    public BoundedUtf8LineReader(Stream stream, long maxBytes, int maxLineBytes, int maxLineChars)
    {
        _stream = stream;
        _maxBytes = maxBytes;
        _maxLineBytes = maxLineBytes;
        _maxLineChars = maxLineChars;
        _lineBuffer = new byte[maxLineBytes];
    }

    public BoundedLineReadResult ReadLine(out string? line)
    {
        int lineLength = 0;
        while (TryReadByte(out byte value))
        {
            if (++_bytesRead > _maxBytes)
            {
                line = null;
                return BoundedLineReadResult.LimitExceeded;
            }

            if (value == (byte)'\n')
            {
                return DecodeLine(lineLength, out line);
            }

            if (lineLength >= _maxLineBytes)
            {
                line = null;
                return BoundedLineReadResult.LimitExceeded;
            }

            _lineBuffer[lineLength++] = value;
        }

        if (lineLength == 0)
        {
            line = null;
            return BoundedLineReadResult.End;
        }

        return DecodeLine(lineLength, out line);
    }

    private BoundedLineReadResult DecodeLine(int lineLength, out string? line)
    {
        if (lineLength > 0 && _lineBuffer[lineLength - 1] == (byte)'\r')
        {
            lineLength--;
        }

        line = Encoding.UTF8.GetString(_lineBuffer, 0, lineLength);
        if (line.Length > _maxLineChars)
        {
            line = null;
            return BoundedLineReadResult.LimitExceeded;
        }

        return BoundedLineReadResult.Line;
    }

    private bool TryReadByte(out byte value)
    {
        if (_readOffset >= _readCount)
        {
            _readCount = _stream.Read(_readBuffer, 0, _readBuffer.Length);
            _readOffset = 0;
            if (_readCount == 0)
            {
                value = default;
                return false;
            }
        }

        value = _readBuffer[_readOffset++];
        return true;
    }
}

#pragma warning restore RS0051
