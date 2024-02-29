// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal sealed class MSTestExtension : IExtension
{
    public string Uid => nameof(MSTestExtension);

    public string DisplayName => "MSTest";

    public string Version => RepositoryVersion.Version;

    public string Description => "MSTest Framework for Microsoft Testing Platform";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
#endif
