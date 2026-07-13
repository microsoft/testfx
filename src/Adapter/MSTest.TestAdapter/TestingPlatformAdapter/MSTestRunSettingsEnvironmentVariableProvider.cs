// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;

using IEnvironment = Microsoft.Testing.Platform.Helpers.IEnvironment;
using IFileSystem = Microsoft.Testing.Platform.Helpers.IFileSystem;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native test host environment-variable provider that applies the <c>&lt;EnvironmentVariables&gt;</c> from a
/// .runsettings file to the test host. Mirrors the VSTest bridge's <c>RunSettingsEnvironmentVariableProvider</c>
/// without depending on the bridge.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestRunSettingsEnvironmentVariableProvider(IExtension extension, ICommandLineOptions commandLineOptions, IFileSystem fileSystem, IEnvironment environment)
    : RunSettingsEnvironmentVariableProviderBase(extension, commandLineOptions, fileSystem, environment);
#endif
