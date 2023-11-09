// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.Configurations;

internal sealed partial class JsonConfigurationSource : IConfigurationSource
{
    private readonly IRuntime _runtime;
    private readonly IFileSystem _fileSystem;
    private readonly FileLoggerProvider? _fileLoggerProvider;

    public JsonConfigurationSource(IRuntime runtime, IFileSystem fileSystem, FileLoggerProvider? fileLoggerProvider)
    {
        _runtime = runtime;
        _fileSystem = fileSystem;
        _fileLoggerProvider = fileLoggerProvider;
    }

    /// <inheritdoc />
    public string Uid { get; } = nameof(JsonConfigurationSource);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = "Test Anywhere JSON Configuration Builder";

    /// <inheritdoc />
    public string Description { get; } = "Built-in default Test Anywhere JSON Configuration Builder";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IConfigurationProvider Build()
        => new JsonConfigurationProvider(_runtime, _fileSystem, _fileLoggerProvider?.CreateLogger(typeof(JsonConfigurationProvider).ToString()));
}
