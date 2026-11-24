// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

internal static class TrxTestResultExtractor
{
    // Cap individual stdout/stderr/stack-trace fields when projecting into the streaming DTO.
    // A single TRX result with multi-MB output trips the serializer's 64 MiB record cap (which exists
    // to detect corruption), at which point ReadAll cannot continue past the offending record because
    // there is no sync marker. Truncating at the source preserves the rest of the run; downstream TRX
    // consumers (Azure DevOps, VS) also choke on multi-MB output fields. The chosen cap is well below
    // the serializer's per-record cap to leave room for other fields and metadata.
    private const int MaxCapturedFieldChars = 1 * 1024 * 1024;

    private const string TruncationSuffix = "\n... [truncated by TRX streaming store]";

    /// <summary>
    /// Projects the subset of <see cref="TestNodeUpdateMessage"/> consumed by the TRX renderer into a
    /// self-contained <see cref="TrxTestResult"/> that no longer references the property bag and can be
    /// serialized to disk.
    /// Caller is expected to have already filtered out null/Discovered/InProgress states.
    /// Properties on <see cref="TestNode"/> that the TRX renderer does not consume (e.g. arbitrary
    /// adapter properties) are intentionally dropped here — adding a new TRX feature that needs them
    /// requires extending this extractor and the serializer / DTO together.
    /// Returns the projected result along with a flag indicating whether any captured text field
    /// (stdout / stderr / stack trace / exception message) was truncated at the per-field cap.
    /// </summary>
    public static (TrxTestResult Result, bool WasTruncated) Extract(TestNodeUpdateMessage message)
    {
        (TrxTestResult result, bool wasTruncated, IReadOnlyList<(string PropertyType, int Count)>? duplicateProperties) = ExtractAndValidate(message);
        return duplicateProperties is [var firstDuplicate, ..]
            ? throw new InvalidOperationException($"Found multiple properties of type '{firstDuplicate.PropertyType}'.")
            : (result, wasTruncated);
    }

    /// <summary>
    /// Same as <see cref="Extract(TestNodeUpdateMessage)"/>, but instead of throwing when a property type that TRX
    /// can only represent once is present several times on the same message, reports every such type (with its
    /// number of occurrences) so that the caller can surface the producer bug without crashing the test session.
    /// When duplicates are reported, the returned result must not be rendered.
    /// </summary>
    public static (TrxTestResult Result, bool WasTruncated, IReadOnlyList<(string PropertyType, int Count)>? DuplicateProperties) ExtractAndValidate(TestNodeUpdateMessage message)
    {
        TestNode testNode = message.TestNode;

        TestNodeStateProperty state = testNode.Properties.Single<TestNodeStateProperty>();
        TrxTestOutcome outcome = MapOutcome(state);

        // Single-pass collection of all non-state properties: replaces 7 × SingleOrDefault<T>()
        // + 2 × OfType<T>() with one GetStructEnumerator() walk, saving 8 linked-list traversals
        // and 2 array allocations per test result. Singleton-typed properties use the local
        // GetSingleOrDefaultValue helper to detect duplicates (which SingleOrDefault<T>() used to
        // reject) and report them to the caller; TestMetadataProperty and FileArtifactProperty are
        // intentionally multi-valued and accumulate into lists.
        TimingProperty? timing = null;
        TrxTestDefinitionName? trxDefName = null;
        TrxFullyQualifiedTypeNameProperty? fqtn = null;
        TestMethodIdentifierProperty? methodId = null;
        TrxExceptionProperty? trxException = null;
        TrxMessagesProperty? trxMessages = null;
        TrxCategoriesProperty? trxCategories = null;
        TrxWorkItemsProperty? trxWorkItems = null;
        List<TrxTestMetadata>? metadata = null;
        List<TrxTestFileArtifact>? artifacts = null;
        Dictionary<Type, int>? duplicateCounts = null;

        PropertyBag.PropertyBagEnumerator enumerator = testNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TimingProperty t: timing = GetSingleOrDefaultValue(timing, t, ref duplicateCounts); break;
                case TrxTestDefinitionName d: trxDefName = GetSingleOrDefaultValue(trxDefName, d, ref duplicateCounts); break;
                case TrxFullyQualifiedTypeNameProperty f: fqtn = GetSingleOrDefaultValue(fqtn, f, ref duplicateCounts); break;
                case TestMethodIdentifierProperty m: methodId = GetSingleOrDefaultValue(methodId, m, ref duplicateCounts); break;
                case TrxExceptionProperty e: trxException = GetSingleOrDefaultValue(trxException, e, ref duplicateCounts); break;
                case TrxMessagesProperty msg: trxMessages = GetSingleOrDefaultValue(trxMessages, msg, ref duplicateCounts); break;
                case TrxCategoriesProperty c: trxCategories = GetSingleOrDefaultValue(trxCategories, c, ref duplicateCounts); break;
                case TrxWorkItemsProperty w: trxWorkItems = GetSingleOrDefaultValue(trxWorkItems, w, ref duplicateCounts); break;
                case TestMetadataProperty md: (metadata ??= []).Add(new TrxTestMetadata { Key = md.Key, Value = md.Value }); break;
                case FileArtifactProperty fa: (artifacts ??= []).Add(new TrxTestFileArtifact { FullPath = fa.FileInfo.FullName }); break;
            }
        }

        // Keeps the first occurrence and counts further occurrences so the caller can report the producer bug.
        static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property, ref Dictionary<Type, int>? duplicateCounts)
            where TProperty : IProperty
        {
            if (existingProperty is null)
            {
                return property;
            }

            duplicateCounts ??= [];
            duplicateCounts[typeof(TProperty)] = duplicateCounts.TryGetValue(typeof(TProperty), out int count) ? count + 1 : 2;
            return existingProperty;
        }

        bool wasTruncated = false;

        List<TrxStreamMessage>? messages = null;
        if (trxMessages?.Messages is { Length: > 0 } srcMessages)
        {
            messages = [];
            foreach (TrxMessage src in srcMessages)
            {
                messages.Add(new TrxStreamMessage
                {
                    Kind = src switch
                    {
                        StandardErrorTrxMessage => TrxStreamMessageKind.StandardError,
                        DebugOrTraceTrxMessage => TrxStreamMessageKind.DebugOrTrace,
                        _ => TrxStreamMessageKind.StandardOutput,
                    },
                    Message = TruncateIfNeeded(src.Message, ref wasTruncated),
                });
            }
        }

        var result = new TrxTestResult
        {
            Uid = testNode.Uid.Value,
            DisplayName = testNode.DisplayName,
            Outcome = outcome,
            StartTime = timing?.GlobalTiming.StartTime,
            EndTime = timing?.GlobalTiming.EndTime,
            Duration = timing?.GlobalTiming.Duration,
            TrxTestDefinitionName = trxDefName?.TestDefinitionName,
            TrxFullyQualifiedTypeName = fqtn?.FullyQualifiedTypeName,
            TestMethodIdentifier = methodId is null
                ? null
                : new TrxTestMethodIdentifier
                {
                    Namespace = methodId.Namespace,
                    TypeName = methodId.TypeName,
                    MethodName = methodId.MethodName,
                },
            ExceptionMessage = TruncateIfNeeded(trxException?.Message, ref wasTruncated),
            ExceptionStackTrace = TruncateIfNeeded(trxException?.StackTrace, ref wasTruncated),
            Messages = messages,
            // Copy the array so the producer's mutable array can't be observed (or mutated) downstream.
            Categories = trxCategories?.Categories is { Length: > 0 } cats ? [.. cats] : null,
            WorkItemIds = trxWorkItems?.WorkItemIds is { Length: > 0 } workItemIds ? [.. workItemIds] : null,
            Metadata = metadata,
            FileArtifacts = artifacts,
        };

        List<(string PropertyType, int Count)>? duplicateProperties = null;
        if (duplicateCounts is not null)
        {
            duplicateProperties = [];
            foreach (KeyValuePair<Type, int> duplicate in duplicateCounts)
            {
                duplicateProperties.Add((duplicate.Key.FullName ?? duplicate.Key.Name, duplicate.Value));
            }

            duplicateProperties.Sort(static (left, right) => string.CompareOrdinal(left.PropertyType, right.PropertyType));
        }

        return (result, wasTruncated, duplicateProperties);
    }

    private static string? TruncateIfNeeded(string? value, ref bool wasTruncated)
    {
        if (value is null || value.Length <= MaxCapturedFieldChars)
        {
            return value;
        }

        // Avoid splitting in the middle of a UTF-16 surrogate pair — otherwise the encoded UTF-8 ends
        // up with an unpaired surrogate which BinaryWriter.Write replaces with U+FFFD on read.
        int sliceLen = MaxCapturedFieldChars;
        if (char.IsHighSurrogate(value[sliceLen - 1]))
        {
            sliceLen--;
        }

        wasTruncated = true;
        return value.Substring(0, sliceLen) + TruncationSuffix;
    }

    private static TrxTestOutcome MapOutcome(TestNodeStateProperty state)
        => state switch
        {
            SkippedTestNodeStateProperty => TrxTestOutcome.Skipped,
            PassedTestNodeStateProperty => TrxTestOutcome.Passed,
            TimeoutTestNodeStateProperty => TrxTestOutcome.Timeout,
            ErrorTestNodeStateProperty or FailedTestNodeStateProperty => TrxTestOutcome.Failed,
            _ => throw ApplicationStateGuard.Unreachable(),
        };
}
