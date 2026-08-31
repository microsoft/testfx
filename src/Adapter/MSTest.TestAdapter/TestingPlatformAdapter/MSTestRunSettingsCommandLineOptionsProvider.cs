// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;

using IFileSystem = Microsoft.Testing.Platform.Helpers.IFileSystem;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native command-line provider for the VSTest <c>--settings</c> (.runsettings) option. Mirrors the VSTest
/// bridge's <c>RunSettingsCommandLineOptionsProvider</c> (identical option name, description and validation) so the
/// <c>--help</c> surface is unchanged.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestRunSettingsCommandLineOptionsProvider : RunSettingsCommandLineOptionsProviderBase
{
    public MSTestRunSettingsCommandLineOptionsProvider(IExtension extension)
        : this(extension, new SystemFileSystem())
    {
    }

    internal MSTestRunSettingsCommandLineOptionsProvider(IExtension extension, IFileSystem fileSystem)
        : base(extension, fileSystem, PlatformAdapterResources.RunSettingsOptionDescription, PlatformAdapterResources.RunsettingsFileDoesNotExist, PlatformAdapterResources.RunsettingsFileCannotBeRead)
    {
    }

    protected override string EnvironmentVariablesNotSupportedOnBrowserError => PlatformAdapterResources.RunSettingsEnvironmentVariablesNotSupportedOnBrowser;
}
#endif
