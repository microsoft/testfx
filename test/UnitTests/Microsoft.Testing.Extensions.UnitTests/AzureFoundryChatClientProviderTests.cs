// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.AI;
using Microsoft.Testing.Extensions.AzureFoundry;

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
    public void IsAvailable_WhenEndpointAndDeploymentSetWithoutApiKey_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        var provider = new AzureOpenAIChatClientProvider();

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

        var provider = new AzureOpenAIChatClientProvider();

        Assert.IsFalse(provider.IsAvailable);
    }

    [TestMethod]
    public void IsAvailable_WhenDeploymentMissing_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");

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

        IChatClient client = await provider.CreateChatClientAsync(CancellationToken.None);

        Assert.IsNotNull(client);
    }

    [TestMethod]
    public async Task CreateChatClientAsync_WithoutApiKey_UsesEntraPathAndReturnsClient()
    {
        Environment.SetEnvironmentVariable(EndpointVariable, "https://contoso.openai.azure.com");
        Environment.SetEnvironmentVariable(DeploymentVariable, "gpt-4o");

        var provider = new AzureOpenAIChatClientProvider();

        // No AZURE_OPENAI_API_KEY is set, so the provider must fall back to DefaultAzureCredential.
        // Credential resolution is lazy, so client construction must not throw even without a live identity.
        IChatClient client = await provider.CreateChatClientAsync(CancellationToken.None);

        Assert.IsNotNull(client);
    }

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
