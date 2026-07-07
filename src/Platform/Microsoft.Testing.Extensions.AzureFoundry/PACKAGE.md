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
- **Flexible authentication**: uses Microsoft Entra ID / managed identity (`DefaultAzureCredential`) by default and falls back to an API key when one is provided

## Configuration

The provider is configured through the following environment variables:

| Variable | Required | Description |
| --- | --- | --- |
| `AZURE_OPENAI_ENDPOINT` | Yes | The Azure OpenAI resource endpoint, for example `https://my-resource.openai.azure.com`. |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | Yes | The name of the model deployment to use. |
| `AZURE_OPENAI_API_KEY` | No | An API key for the resource. When omitted, authentication uses `DefaultAzureCredential` (Managed Identity, Workload Identity, Azure CLI, Visual Studio, etc.). |

When `AZURE_OPENAI_API_KEY` is not set, the provider authenticates with [`DefaultAzureCredential`](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential), which is recommended for Azure-hosted scenarios so that no secret has to be provisioned or distributed. Provide `AZURE_OPENAI_API_KEY` only when you explicitly want key-based authentication.

To target a specific user-assigned managed identity, set the `AZURE_CLIENT_ID` environment variable to its client ID; `DefaultAzureCredential` honors it automatically. Because credentials are resolved lazily, authentication problems (for example, an identity that lacks access to the Azure OpenAI resource) surface on the first chat request rather than when the provider is configured.

> **Note:** In local development, `DefaultAzureCredential` can take several seconds on first use because it probes a chain of credential sources (Managed Identity, Workload Identity, Azure CLI, Visual Studio, etc.) until one succeeds. Setting `AZURE_OPENAI_API_KEY` avoids this delay by using key-based authentication directly.

This package is a reference implementation of the [Microsoft.Testing.Platform.AI](https://www.nuget.org/packages/Microsoft.Testing.Platform.AI) abstractions.

## Related packages

- [Microsoft.Testing.Platform.AI](https://www.nuget.org/packages/Microsoft.Testing.Platform.AI): AI extensibility abstractions for the testing platform

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
