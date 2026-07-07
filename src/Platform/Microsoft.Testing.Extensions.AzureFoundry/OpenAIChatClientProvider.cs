// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ClientModel;

using Azure.AI.OpenAI;
using Azure.Identity;

using Microsoft.Extensions.AI;
using Microsoft.Testing.Extensions.AzureFoundry.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.AI;

namespace Microsoft.Testing.Extensions.AzureFoundry;

/// <summary>
/// Provider for creating Azure OpenAI chat clients.
/// </summary>
internal sealed class AzureOpenAIChatClientProvider : IChatClientProvider
{
    /// <inheritdoc />
    public bool IsAvailable =>
        !RoslynString.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
        !RoslynString.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME"));

    /// <inheritdoc />
    public bool HasToolsCapability => true;

    /// <inheritdoc />
    public string ModelName => Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "unknown";

    /// <inheritdoc />
    public Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken)
    {
        string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string? deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        string? apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        if (RoslynString.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.EnvironmentVariableNotSet, "AZURE_OPENAI_ENDPOINT"));
        }

        if (RoslynString.IsNullOrEmpty(deploymentName))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.EnvironmentVariableNotSet, "AZURE_OPENAI_DEPLOYMENT_NAME"));
        }

        // Prefer an explicit API key when provided, otherwise fall back to Entra ID / managed identity
        // authentication via DefaultAzureCredential. This keeps the provider secure-by-default for
        // Azure-hosted scenarios where distributing API keys is undesirable.
        AzureOpenAIClient client = RoslynString.IsNullOrEmpty(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));

        return Task.FromResult(client.GetChatClient(deploymentName).AsIChatClient());
    }
}
