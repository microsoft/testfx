// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Extensions.DependencyInjection;

internal sealed class MSTestTestClassBuilderConfigurator(IServiceScopeFactory serviceScopeFactory) : ITestingPlatformBuilderConfigurator
{
    public void Configure(ITestApplicationBuilder testApplicationBuilder)
        => TestApplicationBuilderExtensions.SetTestClassInstanceFactory(
            testApplicationBuilder,
            new MicrosoftExtensionsTestClassInstanceFactory(serviceScopeFactory));
}
