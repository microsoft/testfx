// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemRuntimeFeature : IRuntimeFeature
{
#if NETCOREAPP
    public bool IsDynamicCodeSupported => RuntimeFeature.IsDynamicCodeSupported;
#else
    public bool IsDynamicCodeSupported => true;
#endif

    public bool IsHotReloadEnabled { get; private set; }

    public void EnableHotReload() => IsHotReloadEnabled = true;
}
