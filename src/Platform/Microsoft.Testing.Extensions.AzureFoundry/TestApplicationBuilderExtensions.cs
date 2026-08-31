// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Azure.Core;

using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.AzureFoundry;

/// <summary>
/// Extension methods for configuring Azure Foundry services.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class TestApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Azure OpenAI chat client provider to the test application. The provider authenticates with
    /// the API key from the <c>AZURE_OPENAI_API_KEY</c> environment variable.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <remarks>
    /// To authenticate with Entra ID / managed identity, reference the <c>Azure.Identity</c> package and use
    /// the <see cref="AddAzureOpenAIChatClientProvider(ITestApplicationBuilder, TokenCredential)"/> overload,
    /// passing, for example, <c>new DefaultAzureCredential()</c>.
    /// </remarks>
    public static void AddAzureOpenAIChatClientProvider(this ITestApplicationBuilder testApplicationBuilder)
        => testApplicationBuilder.AddChatClientProvider(_ => new AzureOpenAIChatClientProvider());

    /// <summary>
    /// Adds the Azure OpenAI chat client provider to the test application. The provider prefers the API key from
    /// the <c>AZURE_OPENAI_API_KEY</c> environment variable when set, otherwise authenticates with the supplied
    /// <paramref name="credential"/>.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="credential">
    /// The credential used for Entra ID / managed identity authentication, for example
    /// <c>new DefaultAzureCredential()</c> from the <c>Azure.Identity</c> package.
    /// </param>
    public static void AddAzureOpenAIChatClientProvider(this ITestApplicationBuilder testApplicationBuilder, TokenCredential credential)
        => testApplicationBuilder.AddChatClientProvider(_ => new AzureOpenAIChatClientProvider(credential));
}
