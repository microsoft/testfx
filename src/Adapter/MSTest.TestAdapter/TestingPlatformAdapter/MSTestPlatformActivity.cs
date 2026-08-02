// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Bridges the engine's dependency-free <see cref="IMSTestActivity"/> seam onto the platform's OpenTelemetry
/// service, so MSTest fixture and test-method spans nest under the platform's test-case spans.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestPlatformActivity(IPlatformActivity activity) : IMSTestActivity
{
    public void SetTag(string key, object? value)
        => activity.SetTag(key, value);

    public void RecordException(Exception exception)
        => activity.RecordException(exception);

    public void SetFailed(string? description)
        => activity.SetStatus(PlatformActivityStatusCode.Error, description);

    public void Dispose()
        => activity.Dispose();

    /// <summary>
    /// Installs the tracing factory on <see cref="MSTestInstrumentation"/> when the platform has an OpenTelemetry
    /// service registered. Does nothing (leaving MSTest tracing disabled and free) otherwise.
    /// </summary>
    internal static void TryEnable(IServiceProvider serviceProvider)
    {
        if (serviceProvider.GetService(typeof(IPlatformOpenTelemetryService)) is not IPlatformOpenTelemetryService otelService)
        {
            MSTestInstrumentation.SetActivityFactory(null);
            return;
        }

        MSTestInstrumentation.SetActivityFactory((name, tags)
            // A non-ambient span is required, not an optimization - see the remarks on MSTestInstrumentation.
            // Because the span is not ambient it cannot pick a parent up from the ambient context either, so it
            // is parented explicitly to the same test-framework span the platform's test-case spans use, which
            // keeps everything in one trace.
            => otelService.StartNonAmbientActivity(name, tags, otelService.TestFrameworkActivity?.Id) is { } platformActivity
                ? new MSTestPlatformActivity(platformActivity)
                : null);
    }
}
#endif
