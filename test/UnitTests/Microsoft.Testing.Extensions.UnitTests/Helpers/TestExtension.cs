// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class TestExtension : IExtension
{
    public string Uid => "Uid";

    public string Version => "Version";

    public string DisplayName => "DisplayName";

    public string Description => "Description";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
