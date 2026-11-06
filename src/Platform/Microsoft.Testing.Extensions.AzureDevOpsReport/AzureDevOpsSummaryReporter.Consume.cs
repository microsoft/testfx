// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled || value is not TestNodeUpdateMessage update)
            {
                return Task.CompletedTask;
            }

            TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
            TerminalKind kind = SummaryReporterHelpers.GetTerminalKind(state);
            if (kind == TerminalKind.NotTerminal)
            {
                return Task.CompletedTask;
            }

            string uid = update.TestNode.Uid;
            string displayName = update.TestNode.DisplayName;

            // Collect timing and dependency metadata in one pass. TestNodeIdentity resolves the canonical
            // test name separately because it must prefer TestMethodIdentifierProperty over the VSTest fallback.
            TimingProperty? timing = null;
            List<string>? encodedDependencies = null;
            PropertyBag.PropertyBagEnumerator enumerator = update.TestNode.Properties.GetStructEnumerator();
            while (enumerator.MoveNext())
            {
                switch (enumerator.Current)
                {
                    case TimingProperty t: timing = GetSingleOrDefaultValue(timing, t); break;
                    case SerializableKeyValuePairStringProperty kv when kv.Key == MSTestDependencyPropertyKey:
                        (encodedDependencies ??= []).Add(kv.Value);
                        break;
                }
            }

            static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
                where TProperty : class, IProperty
                => existingProperty is not null
                    ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                    : property;

            string fullyQualifiedName = TestNodeIdentity.GetTestName(update.TestNode);
            TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;
            RetryAttemptProperty? retryAttempt = update.TestNode.Properties.SingleOrDefault<RetryAttemptProperty>();
            TestFailureDetails? failure = kind == TerminalKind.Failed ? CaptureFailureDetails(state) : null;
            CiRunSummaryDependency[] dependencies = CreateDependencies(fullyQualifiedName, encodedDependencies);

            lock (_stateLock)
            {
                if (retryAttempt is { IsSuperseded: true })
                {
                    if (kind == TerminalKind.Failed)
                    {
                        _inProcessFailedTests.Add(uid);
                    }

                    return Task.CompletedTask;
                }

                if (kind is TerminalKind.Failed or TerminalKind.Skipped)
                {
                    _notRecoveredTests.Add(uid);
                    ClearFlakyRows(uid);
                }

                bool isFlaky = kind == TerminalKind.Passed
                    && !_notRecoveredTests.Contains(uid)
                    && (_inProcessFailedTests.Contains(uid) || GetAttemptNumber() > 1);
                int finalRowCount = _finalRowCountsByUid.TryGetValue(uid, out int existingFinalRowCount)
                    ? existingFinalRowCount + 1
                    : 1;
                _finalRowCountsByUid[uid] = finalRowCount;
                CiRunSummaryHistoryTest? historyTest = CreateHistoryTest(
                    uid,
                    displayName,
                    fullyQualifiedName,
                    kind,
                    duration,
                    isFlaky);
                _rows.Add(new SummaryRow(
                    uid,
                    new TestRecord(displayName, fullyQualifiedName, kind, duration, isFlaky, failure,
                        kind == TerminalKind.Skipped ? state?.Explanation : null),
                    historyTest,
                    dependencies));
                if (isFlaky)
                {
                    if (!_flakyRowIndicesByUid.TryGetValue(uid, out List<int>? indices))
                    {
                        indices = [];
                        _flakyRowIndicesByUid.Add(uid, indices);
                    }

                    indices.Add(_rows.Count - 1);
                }

                if (finalRowCount > 1)
                {
                    ClearFlakyRows(uid);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(ConsumeAsync), ex);
        }

        return Task.CompletedTask;
    }

    private static TestFailureDetails? CaptureFailureDetails(TestNodeStateProperty? state)
    {
        Exception? exception = state switch
        {
            FailedTestNodeStateProperty failed => failed.Exception,
            ErrorTestNodeStateProperty error => error.Exception,
            TimeoutTestNodeStateProperty timeout => timeout.Exception,
#pragma warning disable CS0618, MTP0001
            CancelledTestNodeStateProperty cancelled => cancelled.Exception,
#pragma warning restore CS0618, MTP0001
            _ => null,
        };
        string? explanation = state?.Explanation;
        string? message = Truncate(
            RoslynString.IsNullOrWhiteSpace(explanation) ? exception?.Message : explanation,
            MaxFailureMessageLength);
        string? exceptionType = exception?.GetType().FullName;
        return RoslynString.IsNullOrWhiteSpace(message)
            && RoslynString.IsNullOrWhiteSpace(exceptionType)
                ? null
                : new TestFailureDetails(message, exceptionType, stackTrace: null, filePath: null, lineNumber: 0);
    }

    private static CiRunSummaryDependency[] CreateDependencies(string dependentFullyQualifiedName, List<string>? encodedDependencies)
    {
        if (encodedDependencies is null)
        {
            return [];
        }

        var dependencies = new List<CiRunSummaryDependency>(Math.Min(encodedDependencies.Count, MaxDependenciesPerTest));
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (string encodedDependency in encodedDependencies.Take(MaxDependencyCandidatesPerTest))
        {
            if (encodedDependency.Length <= MaxDependencyLength
                && TryDecodeDependency(encodedDependency, out string? prerequisite, out bool proceedOnFailure)
                && identities.Add($"{prerequisite}\0{proceedOnFailure}"))
            {
                dependencies.Add(new CiRunSummaryDependency
                {
                    DependentFullyQualifiedName = dependentFullyQualifiedName,
                    Prerequisite = prerequisite,
                    ProceedOnFailure = proceedOnFailure,
                });
                if (dependencies.Count == MaxDependenciesPerTest)
                {
                    break;
                }
            }
        }

        return [.. dependencies];
    }

    private static bool TryDecodeDependency(string encoded, [NotNullWhen(true)] out string? prerequisite, out bool proceedOnFailure)
    {
        prerequisite = null;
        proceedOnFailure = false;
        if (RoslynString.IsNullOrEmpty(encoded))
        {
            return false;
        }

        bool hasFlag = encoded[0] is 'P' or 'S';
        proceedOnFailure = encoded[0] == 'P';
        string payload = hasFlag ? encoded.Substring(1) : encoded;
        int separator = payload.IndexOf('\n');
        string className = separator < 0 ? string.Empty : payload.Substring(0, separator);
        string methodName = separator < 0 ? payload : payload.Substring(separator + 1);
        prerequisite = RoslynString.IsNullOrEmpty(className)
            ? methodName
            : $"{className}.{(RoslynString.IsNullOrEmpty(methodName) ? "*" : methodName)}";
        return !RoslynString.IsNullOrWhiteSpace(prerequisite);
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength) + $"\n…[truncated, original length: {value.Length.ToString(CultureInfo.InvariantCulture)}]";

    private int GetAttemptNumber()
        => int.TryParse(
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int attemptNumber)
            && attemptNumber > 0
                ? attemptNumber
                : 1;
}
