// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

/// <summary>
/// Length-prefixed binary serializer for <see cref="TrxTestResult"/>. Each record is written as
/// [int32 little-endian payload length][payload]. Payload uses <see cref="BinaryWriter"/> primitives:
/// strings as length-prefixed UTF-8 (or single-byte present/absent marker for nullable values),
/// timestamps as ticks + offset minutes, durations as ticks. Lists are int32 count + repeated items.
/// Note: empty collections round-trip as <c>null</c> (the deserializer materializes <c>null</c> when
/// the count is zero); the renderer treats <c>null</c> and empty identically so this normalization
/// is intentional.
/// </summary>
internal static class TrxTestResultSerializer
{
    private const byte AbsentMarker = 0;
    private const byte PresentMarker = 1;

    // Hard cap to detect corrupt headers. Any single TRX record is expected to be far smaller than
    // 64 MiB; a value larger than this indicates header corruption rather than a legitimate large record.
    // Trade-off: a legitimate but pathologically-large record (e.g. multi-MB stack trace + huge stdout
    // capture) will be treated as corruption. We accept that to keep ReadAll deterministic — there is no
    // sync marker so we cannot resync past a record we choose to skip.
    private const int MaxRecordLengthBytes = 64 * 1024 * 1024;

    public static void Write(BinaryWriter writer, TrxTestResult result)
    {
        // Buffer payload to a memory stream first so we can prefix with its length.
        // This makes recovery resilient: a partially-written record can be skipped wholesale.
        using var ms = new MemoryStream(capacity: 256);
        using (var payload = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            payload.Write(result.Uid);
            payload.Write(result.DisplayName);
            payload.Write((byte)result.Outcome);

            WriteNullableDateTimeOffset(payload, result.StartTime);
            WriteNullableDateTimeOffset(payload, result.EndTime);
            WriteNullableTimeSpan(payload, result.Duration);

            WriteNullableString(payload, result.TrxTestDefinitionName);

            WriteNullableString(payload, result.TrxFullyQualifiedTypeName);

            if (result.TestMethodIdentifier is null)
            {
                payload.Write(AbsentMarker);
            }
            else
            {
                payload.Write(PresentMarker);
                payload.Write(result.TestMethodIdentifier.Namespace);
                payload.Write(result.TestMethodIdentifier.TypeName);
                payload.Write(result.TestMethodIdentifier.MethodName);
            }

            WriteNullableString(payload, result.ExceptionMessage);
            WriteNullableString(payload, result.ExceptionStackTrace);

            int messageCount = result.Messages?.Count ?? 0;
            payload.Write(messageCount);
            if (result.Messages is not null)
            {
                foreach (TrxStreamMessage m in result.Messages)
                {
                    payload.Write((byte)m.Kind);
                    WriteNullableString(payload, m.Message);
                }
            }

            int categoryCount = result.Categories?.Count ?? 0;
            payload.Write(categoryCount);
            if (result.Categories is not null)
            {
                foreach (string c in result.Categories)
                {
                    payload.Write(c);
                }
            }

            int metadataCount = result.Metadata?.Count ?? 0;
            payload.Write(metadataCount);
            if (result.Metadata is not null)
            {
                foreach (TrxTestMetadata m in result.Metadata)
                {
                    payload.Write(m.Key);
                    payload.Write(m.Value);
                }
            }

            int artifactCount = result.FileArtifacts?.Count ?? 0;
            payload.Write(artifactCount);
            if (result.FileArtifacts is not null)
            {
                foreach (TrxTestFileArtifact a in result.FileArtifacts)
                {
                    payload.Write(a.FullPath);
                }
            }
        }

        byte[] buffer = ms.GetBuffer();
        int length = (int)ms.Length;
        writer.Write(length);
        writer.Write(buffer, 0, length);
    }

    /// <summary>
    /// Reads all complete records from <paramref name="stream"/>. Stops at end-of-stream or as soon as
    /// a partially-written or corrupt record is detected (which is expected if the writing process crashed
    /// mid-write). The format has no per-record sync marker, so we cannot resync after a corrupt length —
    /// reading on would yield fabricated records from random bytes.
    /// </summary>
    public static IEnumerable<TrxTestResult> ReadAll(Stream stream, ILogger? logger = null)
    {
        while (true)
        {
            // We can't safely use BinaryReader.ReadInt32 at EOF because it throws EndOfStreamException.
            // Peek 4 bytes manually.
            byte[] lenBytes = new byte[4];
            int read = ReadExactly(stream, lenBytes, 0, 4);
            if (read == 0)
            {
                yield break;
            }

            if (read < 4)
            {
                // Partial length prefix: truncated tail, stop.
                yield break;
            }

            int payloadLength = BitConverter.ToInt32(lenBytes, 0);
            if (payloadLength is <= 0 or > MaxRecordLengthBytes)
            {
                // Implausible length: corrupt tail. There is no sync marker so we cannot resync;
                // reading on would yield fabricated records.
                logger?.LogWarning($"TRX streaming store has invalid record length {payloadLength}; stopping read.");
                yield break;
            }

            byte[] payload = new byte[payloadLength];
            int payloadRead = ReadExactly(stream, payload, 0, payloadLength);
            if (payloadRead < payloadLength)
            {
                yield break;
            }

            using var payloadStream = new MemoryStream(payload, writable: false);
            using var payloadReader = new BinaryReader(payloadStream, Encoding.UTF8, leaveOpen: false);
            yield return ReadRecord(payloadReader);
        }
    }

    private static TrxTestResult ReadRecord(BinaryReader r)
    {
        string uid = r.ReadString();
        string displayName = r.ReadString();
        var outcome = (TrxTestOutcome)r.ReadByte();
        DateTimeOffset? startTime = ReadNullableDateTimeOffset(r);
        DateTimeOffset? endTime = ReadNullableDateTimeOffset(r);
        TimeSpan? duration = ReadNullableTimeSpan(r);
        string? trxDefName = ReadNullableString(r);
        string? fqtn = ReadNullableString(r);

        TrxTestMethodIdentifier? methodId = null;
        if (r.ReadByte() == PresentMarker)
        {
            methodId = new TrxTestMethodIdentifier
            {
                Namespace = r.ReadString(),
                TypeName = r.ReadString(),
                MethodName = r.ReadString(),
            };
        }

        string? exceptionMessage = ReadNullableString(r);
        string? exceptionStackTrace = ReadNullableString(r);

        int messageCount = r.ReadInt32();
        List<TrxStreamMessage>? messages = null;
        if (messageCount > 0)
        {
            messages = [];
            for (int i = 0; i < messageCount; i++)
            {
                messages.Add(new TrxStreamMessage
                {
                    Kind = (TrxStreamMessageKind)r.ReadByte(),
                    Message = ReadNullableString(r),
                });
            }
        }

        int categoryCount = r.ReadInt32();
        List<string>? categories = null;
        if (categoryCount > 0)
        {
            categories = [];
            for (int i = 0; i < categoryCount; i++)
            {
                categories.Add(r.ReadString());
            }
        }

        int metadataCount = r.ReadInt32();
        List<TrxTestMetadata>? metadata = null;
        if (metadataCount > 0)
        {
            metadata = [];
            for (int i = 0; i < metadataCount; i++)
            {
                metadata.Add(new TrxTestMetadata
                {
                    Key = r.ReadString(),
                    Value = r.ReadString(),
                });
            }
        }

        int artifactCount = r.ReadInt32();
        List<TrxTestFileArtifact>? artifacts = null;
        if (artifactCount > 0)
        {
            artifacts = [];
            for (int i = 0; i < artifactCount; i++)
            {
                artifacts.Add(new TrxTestFileArtifact { FullPath = r.ReadString() });
            }
        }

        return new TrxTestResult
        {
            Uid = uid,
            DisplayName = displayName,
            Outcome = outcome,
            StartTime = startTime,
            EndTime = endTime,
            Duration = duration,
            TrxTestDefinitionName = trxDefName,
            TrxFullyQualifiedTypeName = fqtn,
            TestMethodIdentifier = methodId,
            ExceptionMessage = exceptionMessage,
            ExceptionStackTrace = exceptionStackTrace,
            Messages = messages,
            Categories = categories,
            Metadata = metadata,
            FileArtifacts = artifacts,
        };
    }

    private static void WriteNullableString(BinaryWriter w, string? value)
    {
        if (value is null)
        {
            w.Write(AbsentMarker);
        }
        else
        {
            w.Write(PresentMarker);
            w.Write(value);
        }
    }

    private static string? ReadNullableString(BinaryReader r)
        => r.ReadByte() == AbsentMarker ? null : r.ReadString();

    private static void WriteNullableDateTimeOffset(BinaryWriter w, DateTimeOffset? value)
    {
        if (value is null)
        {
            w.Write(AbsentMarker);
            return;
        }

        w.Write(PresentMarker);
        w.Write(value.Value.Ticks);
        w.Write((short)value.Value.Offset.TotalMinutes);
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(BinaryReader r)
    {
        if (r.ReadByte() == AbsentMarker)
        {
            return null;
        }

        long ticks = r.ReadInt64();
        short offsetMinutes = r.ReadInt16();
        return new DateTimeOffset(ticks, TimeSpan.FromMinutes(offsetMinutes));
    }

    private static void WriteNullableTimeSpan(BinaryWriter w, TimeSpan? value)
    {
        if (value is null)
        {
            w.Write(AbsentMarker);
            return;
        }

        w.Write(PresentMarker);
        w.Write(value.Value.Ticks);
    }

    private static TimeSpan? ReadNullableTimeSpan(BinaryReader r)
        => r.ReadByte() == AbsentMarker ? null : new TimeSpan(r.ReadInt64());

    private static int ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        int total = 0;
        while (total < count)
        {
            int read = stream.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                return total;
            }

            total += read;
        }

        return total;
    }
}
