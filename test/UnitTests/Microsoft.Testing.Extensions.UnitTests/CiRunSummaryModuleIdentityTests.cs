// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using GitHubCiRunSummaryModule = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModule;
using GitHubCiRunSummaryModuleIdentity = ghactions::Microsoft.Testing.Extensions.CiRunSummaryModuleIdentity;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiRunSummaryModuleIdentityTests
{
    [TestMethod]
    public void HasDuplicateModuleIdentity_SingleModule_IsNotDuplicate()
    {
        CiRunSummaryModule module = CreateModule();

        Assert.IsFalse(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([module], module));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_FirstMatchingModule_IsDuplicateInBothReporters()
    {
        CiRunSummaryModule first = CreateModule();
        CiRunSummaryModule second = CreateModule();
        GitHubCiRunSummaryModule githubFirst = CreateGitHubModule();
        GitHubCiRunSummaryModule githubSecond = CreateGitHubModule();

        Assert.IsTrue(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([first, second], first));
        Assert.IsTrue(GitHubCiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([githubFirst, githubSecond], githubFirst));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_SecondMatchingModule_IsDuplicateInBothReporters()
    {
        CiRunSummaryModule first = CreateModule();
        CiRunSummaryModule second = CreateModule();
        GitHubCiRunSummaryModule githubFirst = CreateGitHubModule();
        GitHubCiRunSummaryModule githubSecond = CreateGitHubModule();

        Assert.IsTrue(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([first, second], second));
        Assert.IsTrue(GitHubCiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([githubFirst, githubSecond], githubSecond));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_DifferentAssemblyName_IsNotDuplicate()
    {
        CiRunSummaryModule module = CreateModule();
        CiRunSummaryModule other = CreateModule(assemblyName: "OtherTests");

        Assert.IsFalse(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([module, other], module));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_DifferentTargetFramework_IsNotDuplicate()
    {
        CiRunSummaryModule module = CreateModule();
        CiRunSummaryModule other = CreateModule(targetFramework: "net8.0");

        Assert.IsFalse(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([module, other], module));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_DifferentArchitecture_IsNotDuplicate()
    {
        CiRunSummaryModule module = CreateModule();
        CiRunSummaryModule other = CreateModule(architecture: "arm64");

        Assert.IsFalse(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([module, other], module));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_AssemblyNameComparisonIsOrdinalCaseSensitive()
    {
        CiRunSummaryModule module = CreateModule(assemblyName: "Tests");
        CiRunSummaryModule other = CreateModule(assemblyName: "tests");

        Assert.IsFalse(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([module, other], module));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_ArchitectureComparisonIsCaseInsensitive()
    {
        CiRunSummaryModule module = CreateModule(architecture: "x64");
        CiRunSummaryModule other = CreateModule(architecture: "X64");

        Assert.IsTrue(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([module, other], module));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_OnlyMatchingPairIsDuplicate()
    {
        CiRunSummaryModule first = CreateModule();
        CiRunSummaryModule second = CreateModule();
        CiRunSummaryModule different = CreateModule(assemblyName: "OtherTests");
        CiRunSummaryModule[] modules = [first, second, different];

        Assert.IsTrue(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity(modules, first));
        Assert.IsTrue(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity(modules, second));
        Assert.IsFalse(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity(modules, different));
    }

    [TestMethod]
    public void HasDuplicateModuleIdentity_EmptyModuleList_IsNotDuplicate()
    {
        CiRunSummaryModule module = CreateModule();

        Assert.IsFalse(CiRunSummaryModuleIdentity.HasDuplicateModuleIdentity([], module));
    }

    private static CiRunSummaryModule CreateModule(
        string assemblyName = "Tests",
        string targetFramework = "net9.0",
        string architecture = "x64")
        => new()
        {
            AssemblyName = assemblyName,
            TargetFramework = targetFramework,
            Architecture = architecture,
        };

    private static GitHubCiRunSummaryModule CreateGitHubModule(
        string assemblyName = "Tests",
        string targetFramework = "net9.0",
        string architecture = "x64")
        => new()
        {
            AssemblyName = assemblyName,
            TargetFramework = targetFramework,
            Architecture = architecture,
        };
}
