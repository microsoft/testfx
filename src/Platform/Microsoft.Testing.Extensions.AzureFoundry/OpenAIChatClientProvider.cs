// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ClientModel;

using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Testing.Extensions.AzureFoundry.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.AI;
using Microsoft.Testing.Platform.Helpers;

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
    /// <param name="environment">The environment service.</param>
    internal AzureOpenAIChatClientProvider(IEnvironment environment)
        => _environment = environment;

    /// <inheritdoc />
    public bool IsAvailable =>
        !RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
        !RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")) &&
        !RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"));

    /// <inheritdoc />
    public bool HasToolsCapability => true;

    /// <inheritdoc />
    public string ModelName => _environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "unknown";

    /// <inheritdoc />
    public Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken)
    {
        string? endpoint = _environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string? deploymentName = _environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        string? apiKey = _environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

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
            new Uri(endpoint!),
            new ApiKeyCredential(apiKey!));

        return Task.FromResult(client.GetChatClient(deploymentName).AsIChatClient());
    }
}
