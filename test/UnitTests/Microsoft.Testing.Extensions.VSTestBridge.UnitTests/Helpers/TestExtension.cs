// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.Helpers;

internal sealed class TestExtension : IExtension
{
    public string Uid { get; } = "Uid";

    public string Version { get; } = "Version";

    public string DisplayName { get; } = "DisplayName";

    public string Description { get; } = "Description";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
