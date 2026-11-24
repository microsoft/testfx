// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Assertion telemetry counters are disabled when the process inherits one of the telemetry opt-out environment
/// variables (as CI agents and the mutation-testing workflow do), which makes <c>TelemetryCollector.TrackAssertionCall</c>
/// unobservable from the public assertions. This initializer evaluates the opt-out while those variables are
/// cleared so that <c>AssertTelemetryTrackingTests</c> can verify the tracking of every assertion whatever the
/// environment is. Nothing is transmitted from this test host: the counters only live in memory and are drained by the
/// MSTest adapter, which this host does not use.
/// </summary>
internal static class TelemetryTestInitializer
{
    private static readonly string[] OptOutVariables = ["TESTINGPLATFORM_TELEMETRY_OPTOUT", "DOTNET_CLI_TELEMETRY_OPTOUT"];

    [ModuleInitializer]
    internal static void EnableAssertionTelemetryCounters()
    {
        string?[] originalValues = new string?[OptOutVariables.Length];
        for (int i = 0; i < OptOutVariables.Length; i++)
        {
            originalValues[i] = Environment.GetEnvironmentVariable(OptOutVariables[i]);
            Environment.SetEnvironmentVariable(OptOutVariables[i], null);
        }

        try
        {
            // The opt-out state is evaluated once per process, by the first call.
            TelemetryCollector.TrackAssertionCall(nameof(TelemetryTestInitializer));
            _ = TelemetryCollector.DrainAssertionCallCounts();
        }
        finally
        {
            for (int i = 0; i < OptOutVariables.Length; i++)
            {
                Environment.SetEnvironmentVariable(OptOutVariables[i], originalValues[i]);
            }
        }
    }
}
