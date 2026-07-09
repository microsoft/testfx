// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Azure.Core;

using Microsoft.Extensions.AI;
using Microsoft.Testing.Extensions.AzureFoundry;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

// The provider reads process-wide AZURE_OPENAI_* environment variables, so these tests must not run
// concurrently with each other. Each test snapshots and restores the variables to avoid leaking state.
[TestClass]
[DoNotParallelize]
public sealed class AzureFoundryChatClientProviderTests
{
    private const string EndpointVariable = "AZURE_OPENAI_ENDPOINT";
    private const string DeploymentVariable = "AZURE_OPENAI_DEPLOYMENT_NAME";
    private const string ApiKeyVariable = "AZURE_OPENAI_API_KEY";

    private string? _originalEndpoint;
    private string? _originalDeployment;
    private string? _originalApiKey;

    [TestInitialize]
    public void TestInitialize()
    {
        _originalEndpoint = Environment.GetEnvironmentVariable(EndpointVariable);
        _originalDeployment = Environment.GetEnvironmentVariable(DeploymentVariable);
        _originalApiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);

        Environment.SetEnvironmentVariable(EndpointVariable, null);
        Environment.SetEnvironmentVariable(DeploymentVariable, null);
        Environment.SetEnvironmentVariable(ApiKeyVariable, null);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, _originalEndpoint);
        Environment.SetEnvironmentVariable(DeploymentVariable, _originalDeployment);
        Environment.SetEnvironmentVariable(ApiKeyVariable, _originalApiKey);
    }

    [TestMethod]
    public void IsAvailable_WhenEndpointAndDeploymentSetWithoutApiKeyOrCredential_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        var provider = new AzureOpenAIChatClientProvider();

        // Without an API key and without an injected credential, the provider cannot authenticate.
        Assert.IsFalse(provider.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenEndpointAndDeploymentSetWithCredential_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        var provider = new AzureOpenAIChatClientProvider(Mock.Of<TokenCredential>());

        // An injected credential makes the provider available even without an API key.
        Assert.IsTrue(provider.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenApiKeyAlsoSet_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");
        Environment.SetEnvironmentVariable(ApiKeyVariable, "secret");

        var provider = new AzureOpenAIChatClientProvider();

        Assert.IsTrue(provider.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenEndpointMissing_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");
        Environment.SetEnvironmentVariable(ApiKeyVariable, "secret");

        var provider = new AzureOpenAIChatClientProvider();

        Assert.IsFalse(provider.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenDeploymentMissing_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(ApiKeyVariable, "secret");

        var provider = new AzureOpenAIChatClientProvider();

        Assert.IsFalse(provider.IsAvailable);
    }

    [TestMethod]
    public void ModelName_ReturnsDeploymentName()
    {
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        var provider = new AzureOpenAIChatClientProvider();

        Assert.AreEqual("gpt-4o", provider.ModelName);
    }

    [TestMethod]
    public void ModelName_WhenDeploymentMissing_ReturnsUnknown()
    {
        var provider = new AzureOpenAIChatClientProvider();

        Assert.AreEqual("unknown", provider.ModelName);
    }

    [TestMethod]
    public void HasToolsCapability_ReturnsTrue()
    {
        var provider = new AzureOpenAIChatClientProvider();

        Assert.IsTrue(provider.HasToolsCapability);
    }

    [TestMethod]
    public async Task CreateChatClientAsync_WithApiKey_UsesApiKeyPathAndReturnsClient()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");
        Environment.SetEnvironmentVariable(ApiKeyVariable, "secret");

        var provider = new AzureOpenAIChatClientProvider();

        // The selection logic must resolve to the API-key path when a key is present...
        Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.ApiKey, AzureOpenAIChatClientProvider.GetAuthenticationMode("secret", null));

        // ...and the client must be constructed without throwing.
        IChatClient client = await provider.CreateChatClientAsync(CancellationToken.None);

        Assert.IsNotNull(client);
    }

    [TestMethod]
    public async Task CreateChatClientAsync_WithCredentialAndNoApiKey_UsesCredentialPathAndReturnsClient()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        TokenCredential credential = Mock.Of<TokenCredential>();
        var provider = new AzureOpenAIChatClientProvider(credential);

        // No AZURE_OPENAI_API_KEY is set, so the provider must use the injected credential.
        Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.TokenCredential, AzureOpenAIChatClientProvider.GetAuthenticationMode(null, credential));

        // Credential resolution is lazy, so client construction must not throw even without a live identity.
        IChatClient client = await provider.CreateChatClientAsync(CancellationToken.None);

        Assert.IsNotNull(client);
    }

    [TestMethod]
    public async Task CreateChatClientAsync_WithoutApiKeyOrCredential_Throws()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        var provider = new AzureOpenAIChatClientProvider();

        // Neither an API key nor a credential is available, so the provider cannot authenticate.
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => provider.CreateChatClientAsync(CancellationToken.None));

        Assert.Contains(ApiKeyVariable, exception.Message);
    }

    [TestMethod]
    public void Constructor_WithNullCredential_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => new AzureOpenAIChatClientProvider(null!));

    [TestMethod]
    public void GetAuthenticationMode_WithApiKey_ReturnsApiKey()
        => Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.ApiKey, AzureOpenAIChatClientProvider.GetAuthenticationMode("secret", null));

    [TestMethod]
    public void GetAuthenticationMode_WithApiKeyAndCredential_ReturnsApiKey()
        => Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.ApiKey, AzureOpenAIChatClientProvider.GetAuthenticationMode("secret", Mock.Of<TokenCredential>()));

    [TestMethod]
    public void GetAuthenticationMode_WithCredentialAndNoApiKey_ReturnsTokenCredential()
        => Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.TokenCredential, AzureOpenAIChatClientProvider.GetAuthenticationMode(null, Mock.Of<TokenCredential>()));

    [TestMethod]
    public void GetAuthenticationMode_WithEmptyApiKeyAndCredential_ReturnsTokenCredential()
        => Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.TokenCredential, AzureOpenAIChatClientProvider.GetAuthenticationMode(string.Empty, Mock.Of<TokenCredential>()));

    [TestMethod]
    public void GetAuthenticationMode_WithoutApiKeyOrCredential_ReturnsNone()
        => Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.None, AzureOpenAIChatClientProvider.GetAuthenticationMode(null, null));

    [TestMethod]
    public void GetAuthenticationMode_WithEmptyApiKeyAndNoCredential_ReturnsNone()
        => Assert.AreEqual(AzureOpenAIChatClientProvider.AuthenticationMode.None, AzureOpenAIChatClientProvider.GetAuthenticationMode(string.Empty, null));

    [TestMethod]
    public async Task CreateChatClientAsync_WhenEndpointMissing_Throws()
    {
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        var provider = new AzureOpenAIChatClientProvider();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => provider.CreateChatClientAsync(CancellationToken.None));

        Assert.Contains(EndpointVariable, exception.Message);
    }

    [TestMethod]
    public async Task CreateChatClientAsync_WhenDeploymentMissing_Throws()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");

        var provider = new AzureOpenAIChatClientProvider();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => provider.CreateChatClientAsync(CancellationToken.None));

        Assert.Contains(DeploymentVariable, exception.Message);
    }
}
