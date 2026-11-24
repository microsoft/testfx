// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestSettingsMutationTests : TestContainer
{
    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            MSTestSettings.Reset();
        }
    }

    public void NewSettingsDoNotConsiderEmptyDataSourcesInconclusiveNorUseCooperativeCancellation()
    {
        var settings = new MSTestSettings();

        settings.ConsiderEmptyDataSourceAsInconclusive.Should().BeFalse();
        settings.CooperativeCancellationTimeout.Should().BeFalse();
    }

    public void DefaultCurrentSettingsDoNotConsiderEmptyDataSourcesInconclusiveNorUseCooperativeCancellation()
    {
        MSTestSettings.Reset();

        MSTestSettings.CurrentSettings.ConsiderEmptyDataSourceAsInconclusive.Should().BeFalse();
        MSTestSettings.CurrentSettings.CooperativeCancellationTimeout.Should().BeFalse();
    }
}
