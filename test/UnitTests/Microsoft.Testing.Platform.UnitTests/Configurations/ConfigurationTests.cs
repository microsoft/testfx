// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class ConfigurationTests : TestBase
{
    public ConfigurationTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [ArgumentsProvider(nameof(GetConfigurationValueFromJsonData))]
    public async ValueTask GetConfigurationValueFromJson(string jsonFileConfig, string key, string? result)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(jsonFileConfig)));
        ConfigurationManager configurationManager = new();
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(
                new SystemRuntime(new SystemRuntimeFeature(), new SystemEnvironment(), new SystemProcessHandler()),
                fileSystem.Object, null));
        IConfiguration configuration = await configurationManager.BuildAsync(new ServiceProvider(), null);
        Assert.AreEqual(result, configuration[key], $"Expected '{result}' found '{configuration[key]}'");
    }

    internal static IEnumerable<(string JsonFileConfig, string Key, string? Result)> GetConfigurationValueFromJsonData()
    {
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump:Enable", "True");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump:enable", "True");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump:Missing", null);
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump", "{\"Enable\": true}");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true} , \"CrashDump2\": {\"Enable\": true}}}}", "TestingPlatform:Troubleshooting:CrashDump", "{\"Enable\": true}");
        yield return ("{\"TestingPlatform\": {\"Troubleshooting\": {\"CrashDump\": {\"Enable\": true}}}}", "TestingPlatform:", null);
        yield return ("{}", "TestingPlatform:Troubleshooting:CrashDump:Enable", null);
        yield return ("{\"TestingPlatform\": [1,2] }", "TestingPlatform:0", "1");
        yield return ("{\"TestingPlatform\": [1,2] }", "TestingPlatform:1", "2");
        yield return ("{\"TestingPlatform\": [1,2] }", "TestingPlatform", "[1,2]");
        yield return ("{\"TestingPlatform\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "TestingPlatform:Array:0", null);
        yield return ("{\"TestingPlatform\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "TestingPlatform:Array:0:Key", "Value");
        yield return ("{\"TestingPlatform\": { \"Array\" : [ {\"Key\" : \"Value\"} , {\"Key\" : 3} ] } }", "TestingPlatform:Array:1:Key", "3");
    }

    public async ValueTask InvalidJson_Fail()
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open)).Returns(new MemoryStream(Encoding.UTF8.GetBytes(string.Empty)));
        ConfigurationManager configurationManager = new();
        configurationManager.AddConfigurationSource(() =>
            new JsonConfigurationSource(
                new SystemRuntime(new SystemRuntimeFeature(), new SystemEnvironment(), new SystemProcessHandler()),
                fileSystem.Object, null));
        await Assert.ThrowsAsync<Exception>(() => configurationManager.BuildAsync(new ServiceProvider(), null));
    }
}
