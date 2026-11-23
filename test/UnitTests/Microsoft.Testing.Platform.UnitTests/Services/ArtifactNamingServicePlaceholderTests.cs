// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

// ArtifactNamingHelper is linked into the test project, so only the product's ArtifactNamingService
// (the single production caller of ResolveAndSanitize) exercises the mutated product copy.
[TestClass]
public sealed class ArtifactNamingServicePlaceholderTests
{
    private static ArtifactNamingService CreateService()
    {
        Mock<ITestApplicationModuleInfo> moduleInfo = new();
        moduleInfo.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns("MyApp.dll");
        Mock<IEnvironment> environment = new();
        environment.SetupGet(x => x.ProcessId).Returns(4242);
        Mock<IClock> clock = new();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        return new ArtifactNamingService(moduleInfo.Object, environment.Object, clock.Object);
    }

    [TestMethod]
    public void ResolveFileName_ArchPlaceholder_ExpandsToTheLowercaseProcessArchitecture()
    {
        string result = CreateService().ResolveFileName("{arch}.txt");

        Assert.AreEqual(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant() + ".txt", result);
    }

    [TestMethod]
    public void ResolveFileName_AsmPlaceholder_ExpandsToTheEntryAssemblyName()
    {
        string? entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        Assert.IsNotNull(entryAssemblyName);

        string result = CreateService().ResolveFileName("{asm}.txt");

        Assert.AreEqual(entryAssemblyName + ".txt", result);
    }

    [TestMethod]
    public void ResolveFileName_TfmPlaceholder_ExpandsToTheEntryAssemblyTargetFramework()
    {
        string result = CreateService().ResolveFileName("{tfm}.txt");

        Assert.StartsWith("net", result);
        Assert.EndsWith(".txt", result);
        Assert.DoesNotContain("{tfm}", result);
        Assert.AreNotEqual("unknown.txt", result);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void ResolveFileName_EmptyOrWhitespaceTemplate_ThrowsWithTheTemplateMessage(string template)
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => CreateService().ResolveFileName(template));

        Assert.StartsWith("Template cannot be null, empty, or whitespace.", exception.Message);
        Assert.AreEqual("template", exception.ParamName);
    }

    [TestMethod]
    public void ResolveFileName_TemplateEndingWithADirectorySeparator_ThrowsBecauseThereIsNoFileName()
    {
        string template = "logs" + Path.DirectorySeparatorChar;

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => CreateService().ResolveFileName(template));

        Assert.StartsWith("The resolved template must produce a file name, not just a directory path.", exception.Message);
        Assert.AreEqual("template", exception.ParamName);
    }
}
