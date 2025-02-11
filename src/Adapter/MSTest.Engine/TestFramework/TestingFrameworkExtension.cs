// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Framework.Adapter;

internal sealed class TestingFrameworkExtension : IExtension
{
    public string Uid => "MSTestEngine";

    public string Version => TestFrameworkConstants.DefaultSemVer;

    public string DisplayName => "MSTest AOT";

    public string Description => "MSTest AOT. This framework allows you to test your code anywhere in any mode (all OSes, all platforms, all configurations...).";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
}
