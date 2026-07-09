// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Services;

[TestClass]
public sealed class ArtifactNamingServiceTests
{
    private readonly Mock<ITestApplicationModuleInfo> _moduleInfoMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<IClock> _clockMock = new();

    private ArtifactNamingService CreateService(string modulePath = "MyApp.dll", int processId = 4242)
    {
        _ = _moduleInfoMock.Setup(x => x.GetCurrentTestApplicationFullPath()).Returns(modulePath);
        _ = _environmentMock.SetupGet(x => x.ProcessId).Returns(processId);
        _ = _clockMock.SetupGet(x => x.UtcNow).Returns(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        return new ArtifactNamingService(_moduleInfoMock.Object, _environmentMock.Object, _clockMock.Object);
    }

    [TestMethod]
    public void ResolveFileName_ExpandsStandardPlaceholders()
    {
        ArtifactNamingService service = CreateService(modulePath: Path.Combine("dir", "MyApp.dll"), processId: 4242);

        string result = service.ResolveFileName("{pname}_{pid}_{time}.json");

        Assert.StartsWith("MyApp_4242_", result);
        Assert.Contains("2026-01-02_03-04-05", result);
        Assert.EndsWith(".json", result);
        Assert.DoesNotContain("{pname}", result);
        Assert.DoesNotContain("{pid}", result);
        Assert.DoesNotContain("{time}", result);
    }

    [TestMethod]
    public void ResolveFileName_SanitizesInvalidCharactersInExpandedName()
    {
        ArtifactNamingService service = CreateService(modulePath: Path.Combine("dir", "My*App.dll"));

        string result = service.ResolveFileName("{pname}.json");

        Assert.AreEqual("My_App.json", result);
    }

    [TestMethod]
    public void ResolveFileName_PreservesDirectoryPortionAndSanitizesLeaf()
    {
        ArtifactNamingService service = CreateService(modulePath: Path.Combine("dir", "My*App.dll"));

        string template = Path.Combine("sub", "dir", "{pname}.json");
        string result = service.ResolveFileName(template);

        Assert.AreEqual(Path.Combine("sub", "dir", "My_App.json"), result);
    }

    [TestMethod]
    public void ResolveFileName_LeavesUnknownPlaceholderAsIs()
    {
        ArtifactNamingService service = CreateService();

        string result = service.ResolveFileName("{unknown}_{pname}.json");

        Assert.Contains("{unknown}", result);
        Assert.Contains("MyApp", result);
    }
}
