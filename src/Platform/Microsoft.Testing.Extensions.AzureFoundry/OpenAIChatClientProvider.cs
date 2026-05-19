// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ClientModel;

using Azure.AI.OpenAI;

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
        !RoslynString.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")) &&
        !RoslynString.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));

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

        if (RoslynString.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.EnvironmentVariableNotSet, "AZURE_OPENAI_API_KEY"));
        }

        var client = new AzureOpenAIClient(
            new Uri(endpoint),
            new ApiKeyCredential(apiKey));

        return Task.FromResult(client.GetChatClient(deploymentName).AsIChatClient());
    }
}
