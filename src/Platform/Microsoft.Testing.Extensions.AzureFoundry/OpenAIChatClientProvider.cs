// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ClientModel;

using Azure.AI.OpenAI;
using Azure.Core;

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
    // Optional credential used for Entra ID / managed identity authentication. When null, the provider
    // can only authenticate with an API key. Keeping the credential injectable means the core package does
    // not need a dependency on Azure.Identity (and its MSAL graph); consumers that want managed identity
    // reference Azure.Identity themselves and pass a credential (for example new DefaultAzureCredential()).
    private readonly TokenCredential? _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIChatClientProvider"/> class that authenticates
    /// with an API key read from the <c>AZURE_OPENAI_API_KEY</c> environment variable.
    /// </summary>
    public AzureOpenAIChatClientProvider()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIChatClientProvider"/> class that authenticates
    /// with the provided <see cref="TokenCredential"/> when no API key is available.
    /// </summary>
    /// <param name="credential">The credential used for Entra ID / managed identity authentication.</param>
    public AzureOpenAIChatClientProvider(TokenCredential credential)
        => _credential = credential ?? throw new ArgumentNullException(nameof(credential));

    /// <summary>
    /// The authentication mode used to create the Azure OpenAI client.
    /// </summary>
    internal enum AuthenticationMode
    {
        /// <summary>
        /// No authentication is available (neither an API key nor a credential was provided).
        /// </summary>
        None,

        /// <summary>
        /// Authenticate with an explicit API key (<c>AZURE_OPENAI_API_KEY</c>).
        /// </summary>
        ApiKey,

        /// <summary>
        /// Authenticate with an injected <see cref="Azure.Core.TokenCredential"/> (for example Entra ID / managed identity).
        /// </summary>
        TokenCredential,
    }

    /// <inheritdoc />
    public bool IsAvailable =>
        !RoslynString.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
        !RoslynString.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")) &&
        GetAuthenticationMode(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"), _credential) != AuthenticationMode.None;

    /// <inheritdoc />
    public bool HasToolsCapability => true;

    /// <inheritdoc />
    public string ModelName => Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "unknown";

    // Prefer an explicit API key when provided, otherwise fall back to the injected credential (if any).
    // When neither is available the provider cannot authenticate, so the caller must supply a credential.
    internal static AuthenticationMode GetAuthenticationMode(string? apiKey, TokenCredential? credential)
        => !RoslynString.IsNullOrEmpty(apiKey)
            ? AuthenticationMode.ApiKey
            : credential is not null
                ? AuthenticationMode.TokenCredential
                : AuthenticationMode.None;

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

        AzureOpenAIClient client = GetAuthenticationMode(apiKey, _credential) switch
        {
            AuthenticationMode.ApiKey => new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey!)),
            AuthenticationMode.TokenCredential => new AzureOpenAIClient(new Uri(endpoint), _credential!),
            _ => throw new InvalidOperationException(ExtensionResources.NoAuthenticationConfigured),
        };

        return Task.FromResult(client.GetChatClient(deploymentName).AsIChatClient());
    }
}
