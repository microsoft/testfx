// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Resources;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.CommandLine;

/// <summary>
/// A command line service provider to support VSTest .runsettings files.
/// </summary>
internal sealed class RunSettingsCommandLineOptionsProvider : RunSettingsCommandLineOptionsProviderBase
{
    public RunSettingsCommandLineOptionsProvider(IExtension extension)
        : this(extension, new SystemFileSystem())
    {
    }

    internal /* for testing purposes */ RunSettingsCommandLineOptionsProvider(IExtension extension, IFileSystem fileSystem)
        : base(extension, fileSystem, ExtensionResources.RunSettingsOptionDescription, ExtensionResources.RunsettingsFileDoesNotExist, ExtensionResources.RunsettingsFileCannotBeRead)
    {
    }

    protected override string EnvironmentVariablesNotSupportedOnBrowserError => ExtensionResources.RunSettingsEnvironmentVariablesNotSupportedOnBrowser;
}
