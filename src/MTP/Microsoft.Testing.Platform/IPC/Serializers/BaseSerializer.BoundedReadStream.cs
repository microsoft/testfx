// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal abstract partial class BaseSerializer
{
    private sealed class BoundedReadStream(Stream stream, long length) : Stream
    {
        private readonly long _start = stream.Position;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; } = length;

        public override long Position
        {
            get => stream.Position - _start;
            set
            {
                if (value < 0 || value > Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                stream.Position = _start + value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = (int)Math.Min(count, Length - Position);
            return bytesToRead == 0 ? 0 : stream.Read(buffer, offset, bytesToRead);
        }

#if NETCOREAPP
        public override int Read(Span<byte> buffer)
        {
            int bytesToRead = (int)Math.Min(buffer.Length, Length - Position);
            return bytesToRead == 0 ? 0 : stream.Read(buffer[..bytesToRead]);
        }
#endif

        public override int ReadByte()
            => Position == Length ? -1 : stream.ReadByte();

        public override long Seek(long offset, SeekOrigin origin)
        {
            long position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            Position = position;
            return Position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
