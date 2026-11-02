# Microsoft.Testing.Extensions.AzureFoundry

Microsoft.Testing.Extensions.AzureFoundry is an AI provider extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that integrates with Azure AI Foundry (Azure OpenAI) to enable AI-powered testing capabilities.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.AzureFoundry` code in the [microsoft/testfx](https://github.com/microsoft/testfx) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.AzureFoundry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Azure AI Foundry integration**: provides an `IChatClientProvider` implementation backed by Azure OpenAI
- **Provider implementation**: supplies chat client capabilities to extensions using `Microsoft.Testing.Platform.AI`
- **Environment-based configuration**: reads Azure OpenAI settings from environment variables
- **Flexible authentication**: authenticates with an API key out of the box, or with any Azure `TokenCredential` (for example Microsoft Entra ID / managed identity) that the consumer supplies

## Configuration

The provider is configured through the following environment variables:

| Variable | Required | Description |
| --- | --- | --- |
| `AZURE_OPENAI_ENDPOINT` | Yes | The Azure OpenAI resource endpoint, for example `https://my-resource.openai.azure.com`. |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | Yes | The name of the model deployment to use. |
| `AZURE_OPENAI_API_KEY` | Conditional | An API key for the resource. Required unless a `TokenCredential` is supplied in code (see below). |

### Authentication

By default the provider authenticates with the API key from `AZURE_OPENAI_API_KEY`:

```csharp
builder.AddAzureOpenAIChatClientProvider();
```

To use Microsoft Entra ID / managed identity instead, reference the [`Azure.Identity`](https://www.nuget.org/packages/Azure.Identity) package in your test project and pass a credential. The API key still takes precedence when `AZURE_OPENAI_API_KEY` is set:

```csharp
using Azure.Identity;

builder.AddAzureOpenAIChatClientProvider(new DefaultAzureCredential());
```

This keeps `Azure.Identity` (and its MSAL dependency graph) out of the package for consumers who only use API keys — they pull no managed-identity assemblies. Consumers who opt into Entra ID reference `Azure.Identity` explicitly.

> **Note:** `DefaultAzureCredential` can take several seconds on first use because it probes a chain of credential sources (Managed Identity, Workload Identity, Azure CLI, Visual Studio, etc.) until one succeeds. Because credentials are resolved lazily, authentication problems (for example, an identity that lacks access to the Azure OpenAI resource) surface on the first chat request rather than when the provider is configured. To target a specific user-assigned managed identity, set the `AZURE_CLIENT_ID` environment variable to its client ID; `DefaultAzureCredential` honors it automatically.

This package is a reference implementation of the [Microsoft.Testing.Platform.AI](https://www.nuget.org/packages/Microsoft.Testing.Platform.AI) abstractions.

## Related packages

- [Microsoft.Testing.Platform.AI](https://www.nuget.org/packages/Microsoft.Testing.Platform.AI): AI extensibility abstractions for the testing platform

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
