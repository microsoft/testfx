// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

internal sealed class SystemExtension : IExtension
{
    public string Uid => "System";

    public string Version => "N/A";

    public string DisplayName => "System";

    public string Description => "System";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
