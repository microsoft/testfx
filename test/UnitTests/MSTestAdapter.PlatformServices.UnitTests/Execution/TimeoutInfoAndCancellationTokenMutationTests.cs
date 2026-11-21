// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TimeoutInfoAndCancellationTokenMutationTests : TestContainer
{
    private const string CooperativeCancellationRunSettings =
        """
        <RunSettings>
          <MSTest>
            <CooperativeCancellationTimeout>true</CooperativeCancellationTimeout>
          </MSTest>
        </RunSettings>
        """;

    public void FromTimeoutAttributeFallsBackToTheCooperativeCancellationSettingWhenTheAttributeDoesNotSetIt()
    {
        MSTestSettings.Reset();
        try
        {
            MSTestSettings.PopulateSettings(CooperativeCancellationRunSettings, logger: null, configuration: null);

            var timeoutInfo = TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(1234));

            timeoutInfo.Timeout.Should().Be(1234);
            timeoutInfo.CooperativeCancellation.Should().BeTrue();
        }
        finally
        {
            MSTestSettings.Reset();
        }
    }

    public void FromTimeoutAttributeKeepsAnExplicitCooperativeCancellationValueOverTheSetting()
    {
        MSTestSettings.Reset();
        try
        {
            MSTestSettings.PopulateSettings(CooperativeCancellationRunSettings, logger: null, configuration: null);

            var timeoutInfo = TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(1234) { CooperativeCancellation = false });

            timeoutInfo.CooperativeCancellation.Should().BeFalse();
        }
        finally
        {
            MSTestSettings.Reset();
        }
    }

    public void FromFixtureSettingsRejectsAnUnknownFixtureKindAndNamesIt()
    {
        Action action = () => TimeoutInfo.FromFixtureSettings((FixtureKind)123);

        action.Should().Throw<NotSupportedException>().WithMessage("Unsupported fixture kind 123");
    }

    public void ThrowIfCancellationRequestedObservesTheOriginalTokenWhenOneWasProvided()
    {
        using var originalSource = new CancellationTokenSource();
        var token = new TestRunCancellationToken(originalSource.Token);

        Action beforeCancellation = token.ThrowIfCancellationRequested;
        beforeCancellation.Should().NotThrow();

        originalSource.Cancel();

        Action afterCancellation = token.ThrowIfCancellationRequested;
        afterCancellation.Should().Throw<OperationCanceledException>();
        token.Canceled.Should().BeFalse();
    }
}
