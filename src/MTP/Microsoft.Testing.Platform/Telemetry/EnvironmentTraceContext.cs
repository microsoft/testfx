// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Reads a W3C trace context from the environment so that a test run started by an already-traced parent
/// (a CI pipeline step, <c>dotnet test</c>, an IDE, or a test host controller) nests under that parent instead
/// of starting an orphan trace.
/// </summary>
/// <remarks>
/// The <c>TRACEPARENT</c> / <c>TRACESTATE</c> environment variables are the de-facto standard used by the
/// OpenTelemetry ecosystem (and by build systems such as Azure Pipelines and GitHub Actions runners that expose
/// them for child processes). We deliberately only read them: exporting the current context to child processes
/// is done separately by the test host controller.
/// </remarks>
internal static class EnvironmentTraceContext
{
    private const int TraceParentLength = 55;

    internal static string? TryGetParentId(IEnvironment environment)
    {
        string? traceParent = GetFirstNonEmpty(
            environment,
            EnvironmentVariableConstants.TRACEPARENT,
            EnvironmentVariableConstants.TESTINGPLATFORM_TRACEPARENT);

        return IsValidTraceParent(traceParent) ? traceParent : null;
    }

    internal static string? TryGetTraceState(IEnvironment environment)
    {
        string? traceState = GetFirstNonEmpty(
            environment,
            EnvironmentVariableConstants.TRACESTATE,
            EnvironmentVariableConstants.TESTINGPLATFORM_TRACESTATE);

        return RoslynString.IsNullOrWhiteSpace(traceState) ? null : traceState;
    }

    private static string? GetFirstNonEmpty(IEnvironment environment, params string[] names)
    {
        foreach (string name in names)
        {
            string? value = environment.GetEnvironmentVariable(name);
            if (!RoslynString.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Validates the <c>version-traceid-spanid-flags</c> shape, so a malformed variable in the environment cannot
    /// poison the whole trace.
    /// </summary>
    /// <remarks>
    /// The rules mirror what <c>System.Diagnostics</c> enforces internally, because when it rejects a parent id it
    /// does so silently and starts a brand new trace instead - which is exactly the hard-to-debug outcome this
    /// validation exists to turn into a clean "no parent". In particular the hex digits must be lowercase and the
    /// version must not be <c>ff</c>.
    /// </remarks>
    internal static bool IsValidTraceParent([NotNullWhen(true)] string? traceParent)
    {
        if (traceParent is null || traceParent.Length != TraceParentLength)
        {
            return false;
        }

        if (traceParent[2] != '-' || traceParent[35] != '-' || traceParent[52] != '-')
        {
            return false;
        }

        // Version 'ff' is explicitly forbidden by the W3C specification.
        if (traceParent[0] == 'f' && traceParent[1] == 'f')
        {
            return false;
        }

        for (int i = 0; i < traceParent.Length; i++)
        {
            if (i is 2 or 35 or 52)
            {
                continue;
            }

            if (!IsLowerCaseHex(traceParent[i]))
            {
                return false;
            }
        }

        // An all-zero trace id or span id is invalid per the W3C specification.
        return !IsAllZeros(traceParent, 3, 32) && !IsAllZeros(traceParent, 36, 16);
    }

    private static bool IsAllZeros(string value, int start, int length)
    {
        for (int i = start; i < start + length; i++)
        {
            if (value[i] != '0')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLowerCaseHex(char c)
        => c is (>= '0' and <= '9') or (>= 'a' and <= 'f');
}
