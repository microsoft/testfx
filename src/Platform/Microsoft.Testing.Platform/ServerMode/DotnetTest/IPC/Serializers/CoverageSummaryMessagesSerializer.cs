// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Platform.IPC.Serializers;

internal sealed class CoverageSummaryMessagesSerializer : NamedPipeSerializer<CoverageSummaryMessages>, INamedPipeSerializer
{
    public override int Id => CoverageSummaryMessagesFieldsId.MessagesSerializerId;

    protected override CoverageSummaryMessages DeserializeCore(Stream stream)
    {
        string? executionId = null;
        string? instanceId = null;
        CoverageSummaryMessage[] summaries = [];

        ReadFields(stream, (fieldId, fieldSize) =>
        {
            if (TryReadExecutionScopedField(stream, fieldId, fieldSize, ref executionId, ref instanceId))
            {
                return true;
            }

            if (fieldId == CoverageSummaryMessagesFieldsId.Summaries)
            {
                summaries = ReadFieldPayload(stream, fieldSize, ReadSummariesPayload);
                return true;
            }

            return false;
        });

        return new(executionId, instanceId, summaries);
    }

    internal static CoverageSummaryMessage[] ReadSummariesPayload(Stream stream)
    {
        int length = ReadCollectionLength(stream, sizeof(ushort));
        var summaries = new CoverageSummaryMessage[length];
        for (int i = 0; i < length; i++)
        {
            string? sessionUid = null, producerId = null, scopeName = null, customMetricName = null;
            int? scopeLevel = null, metric = null;
            long? coveredCount = null, coverableCount = null;

            ReadFields(stream, (fieldId, fieldSize) =>
            {
                switch (fieldId)
                {
                    case CoverageSummaryMessageFieldsId.SessionUid:
                        sessionUid = ReadStringValue(stream, fieldSize);
                        return true;

                    case CoverageSummaryMessageFieldsId.ProducerId:
                        producerId = ReadStringValue(stream, fieldSize);
                        return true;

                    case CoverageSummaryMessageFieldsId.ScopeLevel:
                        scopeLevel = ReadInt(stream);
                        return true;

                    case CoverageSummaryMessageFieldsId.ScopeName:
                        scopeName = ReadStringValue(stream, fieldSize);
                        return true;

                    case CoverageSummaryMessageFieldsId.Metric:
                        metric = ReadInt(stream);
                        return true;

                    case CoverageSummaryMessageFieldsId.CustomMetricName:
                        customMetricName = ReadStringValue(stream, fieldSize);
                        return true;

                    case CoverageSummaryMessageFieldsId.CoveredCount:
                        coveredCount = ReadLong(stream);
                        return true;

                    case CoverageSummaryMessageFieldsId.CoverableCount:
                        coverableCount = ReadLong(stream);
                        return true;

                    default:
                        return false;
                }
            });

            summaries[i] = new(sessionUid, producerId, scopeLevel, scopeName, metric, customMetricName, coveredCount, coverableCount);
        }

        return summaries;
    }

    protected override void SerializeCore(CoverageSummaryMessages objectToSerialize, Stream stream)
    {
        WriteExecutionScopedHeader(
            stream,
            objectToSerialize.ExecutionId,
            objectToSerialize.InstanceId,
            (ushort)(IsNullOrEmpty(objectToSerialize.Summaries) ? 0 : 1));

        WriteSummariesPayload(stream, CoverageSummaryMessagesFieldsId.Summaries, objectToSerialize.Summaries);
    }

    internal static void WriteSummariesPayload(Stream stream, ushort fieldId, CoverageSummaryMessage[]? summaries)
        => WriteListPayload(stream, fieldId, summaries, static (s, summary) =>
        {
            WriteUShort(s, (ushort)(
                (summary.SessionUid is null ? 0 : 1) +
                (summary.ProducerId is null ? 0 : 1) +
                (summary.ScopeLevel is null ? 0 : 1) +
                (summary.ScopeName is null ? 0 : 1) +
                (summary.Metric is null ? 0 : 1) +
                (summary.CustomMetricName is null ? 0 : 1) +
                (summary.CoveredCount is null ? 0 : 1) +
                (summary.CoverableCount is null ? 0 : 1)));

            WriteField(s, CoverageSummaryMessageFieldsId.SessionUid, summary.SessionUid);
            WriteField(s, CoverageSummaryMessageFieldsId.ProducerId, summary.ProducerId);
            WriteField(s, CoverageSummaryMessageFieldsId.ScopeLevel, summary.ScopeLevel);
            WriteField(s, CoverageSummaryMessageFieldsId.ScopeName, summary.ScopeName);
            WriteField(s, CoverageSummaryMessageFieldsId.Metric, summary.Metric);
            WriteField(s, CoverageSummaryMessageFieldsId.CustomMetricName, summary.CustomMetricName);
            WriteField(s, CoverageSummaryMessageFieldsId.CoveredCount, summary.CoveredCount);
            WriteField(s, CoverageSummaryMessageFieldsId.CoverableCount, summary.CoverableCount);
        });
}
