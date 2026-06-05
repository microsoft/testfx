// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.Configurations;

internal abstract class ConfigurationSourceBase : IConfigurationSource
{
    public abstract string Uid { get; }

    public virtual string Version => PlatformVersion.Version;

    // Can be empty string because it's not used in the UI.
    public virtual string DisplayName => string.Empty;

    // Can be empty string because it's not used in the UI.
    public virtual string Description => string.Empty;

    public virtual Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public abstract int Order { get; }

    public abstract Task<IConfigurationProvider> BuildAsync(CommandLineParseResult commandLineParseResult);
}
