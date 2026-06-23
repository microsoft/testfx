// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;

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
        TestNode testNode = message.TestNode;

        TestNodeStateProperty state = testNode.Properties.Single<TestNodeStateProperty>();
        TrxTestOutcome outcome = MapOutcome(state);

        // Single-pass collection of all non-state properties: replaces 7 × SingleOrDefault<T>()
        // + 2 × OfType<T>() with one GetStructEnumerator() walk, saving 8 linked-list traversals
        // and 2 array allocations per test result. Singleton-typed properties use the local
        // GetSingleOrDefaultValue helper to preserve the throw-on-duplicate invariant that
        // SingleOrDefault<T>() provided; TestMetadataProperty and FileArtifactProperty are
        // intentionally multi-valued and accumulate into lists.
        TimingProperty? timing = null;
        TrxTestDefinitionName? trxDefName = null;
        TrxFullyQualifiedTypeNameProperty? fqtn = null;
        TestMethodIdentifierProperty? methodId = null;
        TrxExceptionProperty? trxException = null;
        TrxMessagesProperty? trxMessages = null;
        TrxCategoriesProperty? trxCategories = null;
        List<TrxTestMetadata>? metadata = null;
        List<TrxTestFileArtifact>? artifacts = null;

        PropertyBag.PropertyBagEnumerator enumerator = testNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TimingProperty t: timing = GetSingleOrDefaultValue(timing, t); break;
                case TrxTestDefinitionName d: trxDefName = GetSingleOrDefaultValue(trxDefName, d); break;
                case TrxFullyQualifiedTypeNameProperty f: fqtn = GetSingleOrDefaultValue(fqtn, f); break;
                case TestMethodIdentifierProperty m: methodId = GetSingleOrDefaultValue(methodId, m); break;
                case TrxExceptionProperty e: trxException = GetSingleOrDefaultValue(trxException, e); break;
                case TrxMessagesProperty msg: trxMessages = GetSingleOrDefaultValue(trxMessages, msg); break;
                case TrxCategoriesProperty c: trxCategories = GetSingleOrDefaultValue(trxCategories, c); break;
                case TestMetadataProperty md: (metadata ??= []).Add(new TrxTestMetadata { Key = md.Key, Value = md.Value }); break;
                case FileArtifactProperty fa: (artifacts ??= []).Add(new TrxTestFileArtifact { FullPath = fa.FileInfo.FullName }); break;
            }
        }

        static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
            where TProperty : IProperty
            => existingProperty is not null
                ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                : property;

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
            Metadata = metadata,
            FileArtifacts = artifacts,
        };

        return (result, wasTruncated);
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
            _ when Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, state.GetType()) >= 0 => TrxTestOutcome.Failed,
            _ => throw ApplicationStateGuard.Unreachable(),
        };
}
