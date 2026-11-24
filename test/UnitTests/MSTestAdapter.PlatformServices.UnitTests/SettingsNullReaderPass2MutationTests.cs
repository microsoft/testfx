// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class SettingsNullReaderPass2MutationTests : TestContainer
{
    public void MSTestSettingsRejectsANullXmlReader()
    {
        MethodInfo? method = typeof(MSTestSettings).GetMethod(
            "ToSettings",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(XmlReader), typeof(IAdapterMessageLogger)],
            modifiers: null);
        method.Should().NotBeNull();

        Action action = () => method!.Invoke(null, [null, null]);

        action.Should().ThrowExactly<TargetInvocationException>()
            .WithInnerException<ArgumentNullException>()
            .Which.ParamName.Should().Be("reader");
    }

    public void RunConfigurationSettingsRejectsANullXmlReader()
    {
        MethodInfo? method = typeof(RunConfigurationSettings).GetMethod(
            "ToSettings",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            [typeof(XmlReader)],
            modifiers: null);
        method.Should().NotBeNull();

        Action action = () => method!.Invoke(null, [null]);

        action.Should().ThrowExactly<TargetInvocationException>()
            .WithInnerException<ArgumentNullException>()
            .Which.ParamName.Should().Be("reader");
    }
}
