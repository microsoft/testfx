// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureFoundry;

/// <summary>
/// Extension methods for configuring Azure Foundry services.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class TestApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Azure OpenAI chat client provider to the test application.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    public static void AddAzureOpenAIChatClientProvider(this ITestApplicationBuilder testApplicationBuilder)
        => testApplicationBuilder.AddChatClientProvider(sp => new AzureOpenAIChatClientProvider(sp.GetRequiredService<IEnvironment>()));
}
