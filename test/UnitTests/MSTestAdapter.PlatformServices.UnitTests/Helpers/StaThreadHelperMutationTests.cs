// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "The Windows-only apartment behavior is exercised only on Windows.")]
public sealed class StaThreadHelperMutationTests : TestContainer
{
    public async Task RequestingTheApartmentOfTheCallingStaThread_RunsInlineWithoutRaisingTheNonWindowsWarning()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        List<string> events = [];
        bool ranOnCallingThread = false;
        ApartmentState? actionApartment = null;
        int result = 0;

        await StaThreadHelper.RunOnStaThreadAsync(async () =>
        {
            int caller = Environment.CurrentManagedThreadId;
            result = await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
                ApartmentState.STA,
                "unused",
                () =>
                {
                    ranOnCallingThread = Environment.CurrentManagedThreadId == caller;
                    actionApartment = Thread.CurrentThread.GetApartmentState();
                    events.Add("action");
                    return Task.FromResult(41);
                },
                () => throw new InvalidOperationException("unexpected fallback"),
                _ => throw new InvalidOperationException("unexpected wait"),
                (_, _) => throw new InvalidOperationException("unexpected handler"),
                () => events.Add("warning"));
        });

        result.Should().Be(41);
        ranOnCallingThread.Should().BeTrue("the caller already has the requested apartment state");
        actionApartment.Should().Be(ApartmentState.STA);
        events.Should().Equal("action");
    }

    public async Task RequestingTheMtaApartmentFromAnMtaCaller_RunsInlineWithoutRaisingTheNonWindowsWarning()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        List<string> events = [];
        bool ranOnCallingThread = false;
        int result = 0;

        // Thread pool threads are always MTA, regardless of the apartment state of the test host thread.
        await Task.Run(async () =>
        {
            int caller = Environment.CurrentManagedThreadId;
            result = await StaThreadHelper.RunOnApartmentThreadIfNeededAsync(
                ApartmentState.MTA,
                "unused",
                () =>
                {
                    ranOnCallingThread = Environment.CurrentManagedThreadId == caller;
                    events.Add("action");
                    return Task.FromResult(43);
                },
                () => throw new InvalidOperationException("unexpected fallback"),
                _ => throw new InvalidOperationException("unexpected wait"),
                (_, _) => throw new InvalidOperationException("unexpected handler"),
                () => events.Add("warning"));
        });

        result.Should().Be(43);
        ranOnCallingThread.Should().BeTrue("the caller already has the requested apartment state");
        events.Should().Equal("action");
    }
}
