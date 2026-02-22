# Microsoft.Testing.Platform.AI

Microsoft.Testing.Platform.AI provides the AI extensibility abstractions for [Microsoft.Testing.Platform](https://www.nuget.org/packages/Microsoft.Testing.Platform), enabling test frameworks and extensions to leverage Large Language Models (LLMs) and AI capabilities for intelligent testing activities.

Microsoft.Testing.Platform is open source. You can find `Microsoft.Testing.Platform.AI` code in the [microsoft/testfx](https://github.com/microsoft/testfx/tree/main/src/Platform/Microsoft.Testing.Platform.AI) GitHub repository.

## Install the package

```dotnetcli
dotnet add package Microsoft.Testing.Platform.AI
```

## About

This package provides:

- **IChatClientProvider abstraction**: a standardized interface for AI providers to integrate with the testing platform
- **Unified AI access**: allows any extension (crash dump analysis, hang analysis, test failure root cause analysis, etc.) to access AI capabilities without implementing provider-specific logic
- **Microsoft.Extensions.AI integration**: leverages the industry-standard [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) abstractions for broad AI provider compatibility

> **Note**: This package provides the abstractions only. You need an AI provider implementation (such as [Microsoft.Testing.Extensions.AzureFoundry](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureFoundry)) to supply the actual AI capabilities.

## Related packages

- [Microsoft.Testing.Extensions.AzureFoundry](https://www.nuget.org/packages/Microsoft.Testing.Extensions.AzureFoundry): Azure AI Foundry (Azure OpenAI) provider implementation

## Documentation

For comprehensive documentation, see <https://aka.ms/testingplatform>.

## Feedback & contributing

Microsoft.Testing.Platform is an open source project. Provide feedback or report issues in the [microsoft/testfx](https://github.com/microsoft/testfx/issues) GitHub repository.
