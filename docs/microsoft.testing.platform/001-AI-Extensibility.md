# RFC 001 - AI-Powered Extensibility for Testing Platform

- [ ] Approved in principle
- [ ] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

This RFC details the Microsoft.Testing.Platform extensibility model for integrating Large Language Models (LLMs) and AI capabilities into test frameworks and extensions. The goal is to provide a standardized way for any extensibility point in the testing platform to leverage AI for intelligent testing activities such as flaky test analysis, crash dump analysis, hang analysis, test failure root cause analysis, and more.

## Motivation

Modern testing scenarios increasingly benefit from AI-powered analysis and automation. Various testing extensibility points could leverage LLMs to provide intelligent insights:

1. **Test Frameworks** - Analyze flaky tests, suggest test improvements, generate test cases, analyze test failures for root causes
2. **Crash Dump Extensions** - Automatically analyze crash dumps to identify root causes, stack trace analysis, memory corruption detection
3. **Hang Analysis Extensions** - Analyze hanging processes, identify deadlocks, suggest fixes
4. **Code Coverage Extensions** - Suggest areas needing more test coverage, identify critical untested paths
5. **Performance Extensions** - Analyze performance regressions, suggest optimizations
6. **Test Result Extensions** - Summarize test results, identify patterns in failures

However, each extension implementing its own AI integration would lead to:

- **Duplication** - Multiple extensions reimplementing the same AI client initialization logic
- **Inconsistency** - Different configuration patterns across extensions
- **Complexity** - Users having to configure AI settings multiple times for different extensions
- **Limited Flexibility** - Difficulty in switching between different AI providers (Azure OpenAI, OpenAI, local models, etc.)

The platform needs a unified, extensible approach to AI integration that allows extensions to access AI capabilities in a standardized way.

## Detailed Design

### Requirements

1. Extensions should be able to access AI chat clients without implementing provider-specific logic
2. The platform should support multiple AI providers (Azure OpenAI, OpenAI, local models, etc.)
3. Only one AI provider should be active at a time to avoid conflicts and unnecessary resource consumption
4. AI provider availability should be detectable at runtime (e.g., if required environment variables are missing)
5. Extensions should be able to determine if the AI provider supports advanced features like tool calling
6. The design should leverage existing industry-standard abstractions (Microsoft.Extensions.AI)
7. Configuration should be simple and consistent across all extensions

### Proposed Solution

The solution introduces a **Chat Client Provider** abstraction that allows AI providers to be registered with the testing platform and consumed by any extension.

#### Core Components

##### 1. IChatClientProvider Interface

This interface defines the contract for AI provider implementations:

```csharp
namespace Microsoft.Testing.Platform.AI;

public interface IChatClientProvider
{
    /// <summary>
    /// Gets a value indicating whether the provider is available and can be used.
    /// A provider may be registered but not available if required configuration
    /// (e.g., environment variables) is missing.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets a value indicating whether the chat client supports tool calling
    /// (e.g., MCP tools or functions).
    /// </summary>
    bool SupportsToolCalling { get; }

    /// <summary>
    /// Gets the name of the model being used by the chat client.
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Creates a new instance of IChatClient.
    /// </summary>
    Task<IChatClient> CreateChatClientAsync(CancellationToken cancellationToken);
}
```

**Design Decisions:**

- Uses `Microsoft.Extensions.AI.IChatClient` as the return type, leveraging the industry-standard abstraction
- `IsAvailable` allows providers to validate configuration before being used
- `SupportsToolCalling` enables extensions to use advanced features when available
- `ModelName` provides transparency about which AI model is being used

##### 2. ChatClientManager (Internal)

The platform includes an internal manager that handles provider registration:

```csharp
internal interface IChatClientManager
{
    void AddChatClientProvider(Func<IServiceProvider, object> chatClientProvider);
    void RegisterChatClientProvider(ServiceProvider serviceProvider);
}
```

**Design Decisions:**

- Only one provider can be registered at a time (enforced with an exception)
- Provider registration is deferred until the service provider is fully built
- The manager integrates with the existing platform service provider pattern

##### 3. Extension Methods for Registration

Test application builders can register AI providers using extension methods:

```csharp
namespace Microsoft.Testing.Platform.AI;

public static class ChatClientProviderExtensions
{
    /// <summary>
    /// Adds a chat client provider to the test application builder.
    /// </summary>
    public static void AddChatClientProvider(
        this ITestApplicationBuilder testApplicationBuilder,
        Func<IServiceProvider, IChatClientProvider> chatClientProvider);

    /// <summary>
    /// Gets a chat client from the service provider.
    /// </summary>
    public static async Task<IChatClient?> GetChatClientAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
```

**Design Decisions:**

- `AddChatClientProvider` follows the platform's existing builder pattern
- `GetChatClientAsync` provides a simple way for extensions to access the chat client
- Returns `null` if no provider is registered, allowing graceful degradation

##### 4. Reference Implementation - Azure OpenAI Provider

The platform includes a reference implementation for Azure OpenAI:

```csharp
namespace Microsoft.Testing.Extensions.AzureFoundry;

public static class TestApplicationBuilderExtensions
{
    public static void AddAzureOpenAIChatClientProvider(
        this ITestApplicationBuilder testApplicationBuilder)
        => testApplicationBuilder.AddChatClientProvider(
            sp => new AzureOpenAIChatClientProvider(sp));
}
```

The Azure OpenAI provider:

- Reads configuration from environment variables (`AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_NAME`, `AZURE_OPENAI_API_KEY`)
- Validates availability based on configuration presence
- Supports tool calling
- Uses the official Azure OpenAI SDK

### Usage Example

#### Test Framework with Flaky Test Analysis

```csharp
public class MyTestFramework : ITestFramework
{
    private readonly IServiceProvider _serviceProvider;

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        // Execute tests
        var results = await ExecuteTestsAsync(context);

        // Analyze flaky tests using AI
        IChatClientProvider? provider = _serviceProvider
            .GetServiceInternal<IChatClientProvider>();

        if (provider?.IsAvailable == true)
        {
            IChatClient chatClient = await provider
                .CreateChatClientAsync(context.CancellationToken);

            var flakyTests = results.Where(r => r.IsFlaky);
            if (flakyTests.Any())
            {
                var analysis = await AnalyzeFlakyTestsAsync(
                    chatClient,
                    flakyTests,
                    context.CancellationToken);

                // Report analysis
                await ReportFlakyAnalysisAsync(analysis);
            }
        }
    }

    private async Task<string> AnalyzeFlakyTestsAsync(
        IChatClient chatClient,
        IEnumerable<TestResult> flakyTests,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
            Analyze these flaky tests and suggest potential root causes:
            {string.Join("\n", flakyTests.Select(t => t.ToString()))}
            """;

        var response = await chatClient.GetResponseAsync(
            prompt,
            cancellationToken: cancellationToken);

        return response.Text;
    }
}
```

### Benefits

#### For Extension Authors

1. **No AI-specific infrastructure** - Extensions only need to call `GetChatClientAsync()` and use the standard `IChatClient` interface
2. **Works with any provider** - Extension works automatically with Azure OpenAI, OpenAI, local models, or any custom provider
3. **Graceful degradation** - Extension can detect when AI is unavailable and skip AI-powered features
4. **Feature detection** - Extensions can check `SupportsToolCalling` and use advanced features when available

#### For Test Authors

1. **Single configuration point** - Configure AI provider once, all extensions use it
2. **Easy provider switching** - Change from Azure OpenAI to OpenAI by swapping one line
3. **Clear availability** - Know which features are available based on configuration
4. **Consistent experience** - All AI-powered features use the same underlying provider

#### For the Ecosystem

1. **Reusable providers** - AI providers can be packaged and shared as NuGet packages
2. **Standardization** - All extensions follow the same AI integration pattern
3. **Interoperability** - Leverages `Microsoft.Extensions.AI` for broad ecosystem compatibility
4. **Extensibility** - New providers can be added without changing existing extensions

### Integration with Testing Platform

The AI provider is registered during test host initialization:

```csharp
// In TestHostBuilder.BuildAsync()
public async Task<IHost> BuildAsync()
{
    // ... existing initialization ...

    // Register AI provider if configured
    ChatClientManager.RegisterChatClientProvider(serviceProvider);

    // ... continue with normal flow ...
}
```

This ensures:

- The provider is available when any extension needs it
- Registration happens after all services are configured
- The service provider pattern is consistent with the rest of the platform

## Architecture: Keeping the Core Platform Dependency-Free

A critical design principle of this feature is maintaining the independence of the core `Microsoft.Testing.Platform` library. The platform serves as the foundational pillar for the entire ecosystem and must remain lightweight and free from external dependencies.

### Separation of Concerns

The AI extensibility is implemented through a separate `Microsoft.Testing.Platform.AI` library that acts as a bridge between:

1. **AI Provider Implementations** - Concrete implementations like `Microsoft.Testing.Extensions.AzureFoundry` that integrate with specific AI services
2. **Core Platform Service Provider** - The platform's service provider mechanism that makes services available to extensions

This architectural separation provides several key benefits:

**For the Core Platform:**

- `Microsoft.Testing.Platform` remains dependency-free and does not reference AI-related packages like `Microsoft.Extensions.AI`
- The core platform stays lean, fast to load, and suitable for all testing scenarios (including those that don't need AI)
- No breaking changes or version conflicts from AI SDK updates

**For AI Capabilities:**

- The `Microsoft.Testing.Platform.AI` library defines the public contract (`IChatClientProvider`) without coupling to specific AI implementations
- Extensions can opt-in to AI capabilities by referencing only the packages they need
- Provider implementations (e.g., Azure OpenAI, OpenAI, local models) can evolve independently

**For the Integration:**

- Internal management components in the core platform (`ChatClientManager`) handle provider registration
- The AI library bridges provider implementations to the service provider
- Extensions receive a fully configured `IChatClient` through the standard service provider pattern

This design ensures that the core testing platform remains a stable, dependency-free foundation while enabling rich AI-powered extensibility for those who need it.
