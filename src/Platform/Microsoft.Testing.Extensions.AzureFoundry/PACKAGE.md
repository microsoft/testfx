# Microsoft.Testing.Extensions.AzureFoundry

Microsoft.Testing.Extensions.AzureFoundry is an AI provider extension for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform) that integrates with Azure AI Foundry (Azure OpenAI) to enable AI-powered testing capabilities.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Extensions.AzureFoundry` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Extensions.AzureFoundry) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Extensions.AzureFoundry
```

## About

This package extends Microsoft.Testing.Platform with:

- **Azure AI Foundry integration**: provides an `IChatClientProvider` implementation backed by Azure OpenAI
- **Provider implementation**: supplies chat client capabilities to extensions using `Microsoft.Testing.Platform.AI`
- **Environment-based configuration**: reads Azure OpenAI settings from environment variables

This package is a reference implementation of the [Microsoft.Testing.Platform.AI](https://www.nuget.org/packages/Microsoft.Testing.Platform.AI) abstractions.

## Related packages

- [Microsoft.Testing.Platform.AI](https://www.nuget.org/packages/Microsoft.Testing.Platform.AI): AI extensibility abstractions for the testing platform

## Documentation

For this extension, see <https://github.com/microsoft/testfx/blob/main/docs/microsoft.testing.platform/001-AI-Extensibility.md>.

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
