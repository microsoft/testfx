﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal interface IRuntimeFeature
{
    bool IsDynamicCodeSupported { get; }

    bool IsHotReloadSupported { get; }

    bool IsHotReloadEnabled { get; }
}
