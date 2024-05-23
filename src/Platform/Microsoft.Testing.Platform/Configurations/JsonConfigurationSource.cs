// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource(ITestApplicationModuleInfo testApplicationModuleInfo, IFileSystem fileSystem, FileLoggerProvider? fileLoggerProvider) : IConfigurationSource
{
    /// <inheritdoc />
    public string Uid { get; } = nameof(JsonConfigurationSource);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    // Can be empty string because it's not used in the UI
    public string DisplayName { get; } = string.Empty;

    /// <inheritdoc />
    // Can be empty string because it's not used in the UI
    public string Description { get; } = string.Empty;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IConfigurationProvider Build()
        => new JsonConfigurationProvider(testApplicationModuleInfo, fileSystem, fileLoggerProvider?.CreateLogger(typeof(JsonConfigurationProvider).ToString()));
}
