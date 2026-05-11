// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests.Helpers;

internal class TestExtension(string uid = "Uid") : IExtension
{
    public string Uid { get; } = uid;

    public string Version => "Version";

    public string DisplayName => "DisplayName";

    public string Description => "Description";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
