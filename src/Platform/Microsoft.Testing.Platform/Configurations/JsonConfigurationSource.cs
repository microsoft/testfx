// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource(ITestApplicationModuleInfo testApplicationModuleInfo, IFileSystem fileSystem, FileLoggerProvider? fileLoggerProvider) : IConfigurationSource
{
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly FileLoggerProvider? _fileLoggerProvider = fileLoggerProvider;

    /// <inheritdoc />
    public string Uid => nameof(JsonConfigurationSource);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    // Can be empty string because it's not used in the UI
    public string DisplayName => string.Empty;

    /// <inheritdoc />
    // Can be empty string because it's not used in the UI
    public string Description => string.Empty;

    public int Order => 3;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult)
        => Task.FromResult((IConfigurationProvider)new JsonConfigurationProvider(_testApplicationModuleInfo, _fileSystem, commandLineParseResult, _fileLoggerProvider?.CreateLogger(typeof(JsonConfigurationProvider).ToString())));
}
