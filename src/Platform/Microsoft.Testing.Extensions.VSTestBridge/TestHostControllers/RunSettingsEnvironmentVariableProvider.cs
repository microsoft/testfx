// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.VSTestBridge.TestHostControllers;

internal sealed class RunSettingsEnvironmentVariableProvider(IExtension extension, ICommandLineOptions commandLineOptions, IFileSystem fileSystem, IEnvironment environment)
    : RunSettingsEnvironmentVariableProviderBase(extension, commandLineOptions, fileSystem, environment);
