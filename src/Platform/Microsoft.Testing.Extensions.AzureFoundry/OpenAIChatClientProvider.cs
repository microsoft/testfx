// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ClientModel;

using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Testing.Extensions.AzureFoundry.Resources;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureFoundry;

/// <summary>
/// Provider for creating Azure OpenAI chat clients.
/// </summary>
internal sealed class AzureOpenAIChatClientProvider : IChatClientProvider
{
    private readonly IEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIChatClientProvider"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    internal AzureOpenAIChatClientProvider(IServiceProvider serviceProvider)
    {
        _environment = serviceProvider.GetRequiredService<IEnvironment>();
    }

    /// <inheritdoc />
    public bool SupportsToolCalling => true;

    /// <inheritdoc />
    public string ModelName => _environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "unknown";

    /// <inheritdoc />
    public Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken)
    {
        string? endpoint = _environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string? deploymentName = _environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        string? apiKey = _environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException(ExtensionResources.AzureOpenAIEndpointNotSet);
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            throw new InvalidOperationException(ExtensionResources.AzureOpenAIDeploymentNameNotSet);
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(ExtensionResources.AzureOpenAIApiKeyNotSet);
        }

        var client = new AzureOpenAIClient(
            new Uri(endpoint!),
            new ApiKeyCredential(apiKey!));

        return Task.FromResult(client.GetChatClient(deploymentName).AsIChatClient());
    }
}
