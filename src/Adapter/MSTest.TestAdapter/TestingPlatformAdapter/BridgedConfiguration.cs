﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Configurations;

using PlatformServicesConfiguration = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.IConfiguration;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class BridgedConfiguration : PlatformServicesConfiguration
{
    private readonly IConfiguration _configuration;

    public BridgedConfiguration(IConfiguration configuration)
        => _configuration = configuration;

    public string? this[string key] => _configuration[key];
}
#endif
