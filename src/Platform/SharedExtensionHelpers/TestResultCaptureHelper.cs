// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;

namespace Microsoft.Testing.Extensions;

internal static class TestResultCaptureHelper
{
    internal const int MaxStandardStreamLength = 32 * 1024;
    internal const int MaxStackTraceLength = 32 * 1024;
    internal const int MaxMessageLength = 16 * 1024;
    internal const int MaxIdentityFieldLength = 4 * 1024;
    internal const int MaxTraitFieldLength = 1024;

    internal static CapturedTestResultCoreData? TryCaptureCore(TestNode node, bool includeLocation = false)
    {
        TestNodeStateProperty? state = node.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
        {
            return null;
        }

        CapturedTestResultProperties properties = ExtractProperties(node.Properties, includeLocation);
        TimeSpan duration = properties.Timing?.GlobalTiming.Duration ?? TimeSpan.Zero;
        (string? className, string? methodName) = GetClassAndMethodName(properties.Identifier);
        CapturedExceptionDetails exceptionDetails = ExtractExceptionDetails(state);

        return new CapturedTestResultCoreData(state, properties, duration, className, methodName, exceptionDetails);
    }

    internal static CapturedTestResultProperties ExtractProperties(PropertyBag propertyBag, bool includeLocation = false)
    {
        TimingProperty? timing = null;
        TestMethodIdentifierProperty? identifier = null;
        StandardOutputProperty? standardOutput = null;
        StandardErrorProperty? standardError = null;
        TestFileLocationProperty? location = null;
        List<KeyValuePair<string, string>>? traits = null;

        PropertyBag.PropertyBagEnumerator enumerator = propertyBag.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TimingProperty t: timing = GetSingleOrDefaultValue(timing, t); break;
                case TestMethodIdentifierProperty m: identifier = GetSingleOrDefaultValue(identifier, m); break;
                case StandardOutputProperty so: standardOutput = GetSingleOrDefaultValue(standardOutput, so); break;
                case StandardErrorProperty se: standardError = GetSingleOrDefaultValue(standardError, se); break;
                case TestFileLocationProperty loc when includeLocation: location = GetSingleOrDefaultValue(location, loc); break;
                case TestMetadataProperty meta:
                    traits ??= [];
                    traits.Add(new KeyValuePair<string, string>(
                        Truncate(meta.Key, MaxTraitFieldLength)!,
                        Truncate(meta.Value, MaxTraitFieldLength)!));
                    break;
            }
        }

        return new CapturedTestResultProperties(timing, identifier, standardOutput, standardError, location, traits);

        static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
            where TProperty : class, IProperty
            => existingProperty is not null
                ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                : property;
    }

    internal static CapturedExceptionDetails ExtractExceptionDetails(TestNodeStateProperty state)
    {
        string? errorMessage = state.Explanation;
        string? stackTrace = null;
        string? exceptionType = null;
        Exception? exception = state switch
        {
            FailedTestNodeStateProperty f => f.Exception,
            ErrorTestNodeStateProperty e => e.Exception,
            TimeoutTestNodeStateProperty t => t.Exception,
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
            CancelledTestNodeStateProperty c => c.Exception,
#pragma warning restore CS0618, MTP0001
            _ => null,
        };

        if (exception is not null)
        {
            errorMessage ??= exception.Message;
            stackTrace = exception.StackTrace;
            exceptionType = exception.GetType().FullName;
        }

        return new CapturedExceptionDetails(errorMessage, stackTrace, exceptionType);
    }

    internal static string ClassifyOutcome(TestNodeStateProperty state)
        => state switch
        {
            PassedTestNodeStateProperty => "passed",
            SkippedTestNodeStateProperty => "skipped",
            TimeoutTestNodeStateProperty => "timedOut",
            ErrorTestNodeStateProperty => "errored",
            FailedTestNodeStateProperty => "failed",
            _ when Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, state.GetType()) >= 0
                => "failed",
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    internal static (string? ClassName, string? MethodName) GetClassAndMethodName(TestMethodIdentifierProperty? identifier)
    {
        if (identifier is null)
        {
            return (null, null);
        }

        string className = RoslynString.IsNullOrEmpty(identifier.Namespace)
            ? identifier.TypeName
            : $"{identifier.Namespace}.{identifier.TypeName}";

        return (className, identifier.MethodName);
    }

    internal static string? Truncate(string? value, int maxLength)
    {
        if (value is null || value.Length <= maxLength)
        {
            return value;
        }

        // Don't split a surrogate pair when truncating: drop the high surrogate too.
        // The early return above guarantees cut < value.Length here, but we keep the
        // explicit guard so the invariant is locally self-documenting and survives
        // any future refactor that might call this block with cut == value.Length.
        int cut = maxLength;
        if (cut > 0 && cut < value.Length && char.IsHighSurrogate(value[cut - 1]))
        {
            cut--;
        }

        return value.Substring(0, cut)
            + $"\n…[truncated, original length: {value.Length.ToString(CultureInfo.InvariantCulture)}]";
    }
}

internal readonly record struct CapturedTestResultProperties(
    TimingProperty? Timing,
    TestMethodIdentifierProperty? Identifier,
    StandardOutputProperty? StandardOutput,
    StandardErrorProperty? StandardError,
    TestFileLocationProperty? Location,
    IReadOnlyList<KeyValuePair<string, string>>? Traits);

internal readonly record struct CapturedExceptionDetails(
    string? ErrorMessage,
    string? StackTrace,
    string? ExceptionType);

internal readonly record struct CapturedTestResultCoreData(
    TestNodeStateProperty State,
    CapturedTestResultProperties Properties,
    TimeSpan Duration,
    string? ClassName,
    string? MethodName,
    CapturedExceptionDetails ExceptionDetails);
