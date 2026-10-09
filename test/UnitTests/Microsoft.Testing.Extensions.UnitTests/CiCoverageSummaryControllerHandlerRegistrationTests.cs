// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Reflection;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiCoverageSummaryControllerHandlerRegistrationTests
{
    [TestMethod]
    public async Task AddAzureDevOpsProvider_RegistersRunCompletionHandler()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        builder.AddAzureDevOpsProvider();

        Assert.HasCount(1, GetRunCompletionHandlerFactories(builder));
    }

    [TestMethod]
    public async Task AddGitHubActionsProvider_RegistersRunCompletionHandler()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);

        ghactions::Microsoft.Testing.Extensions.GitHubActionsExtensions.AddGitHubActionsProvider(builder);

        Assert.HasCount(1, GetRunCompletionHandlerFactories(builder));
    }

    private static List<Func<IServiceProvider, ITestHostControllerRunCompletionHandler>> GetRunCompletionHandlerFactories(ITestApplicationBuilder builder)
    {
        FieldInfo factoriesField = typeof(TestHostControllersManager).GetField(
            "_runCompletionHandlerFactories",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<Func<IServiceProvider, ITestHostControllerRunCompletionHandler>>)factoriesField.GetValue(builder.TestHostControllers)!;
    }
}
