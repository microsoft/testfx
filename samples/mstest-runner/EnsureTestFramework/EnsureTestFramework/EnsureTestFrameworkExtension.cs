// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using Microsoft.Testing.Platform.Extensions;

namespace Contoso.EnsureTestFramework;

internal sealed class EnsureTestFrameworkExtension : IExtension
{
    public string Uid => nameof(EnsureTestFrameworkExtension);

    public string DisplayName => "Contoso.Ensure";

    public string Version => "1.0.0";

    public string Description => "An example of a non-bridged test framework.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
