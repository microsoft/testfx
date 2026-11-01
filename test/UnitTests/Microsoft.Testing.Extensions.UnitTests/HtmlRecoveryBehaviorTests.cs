// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Nodes;

using Microsoft.Testing.Extensions.HtmlReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class HtmlRecoveryBehaviorTests
{
    [TestMethod]
    public async Task ControllerExit_RendersRecoveredHtmlWithOriginalMetadata()
    {
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        builder.AddHtmlReportProvider();
        var fixture = new ReportBehaviorFixture(new()
        {
            ["report-html"] = [],
            ["report-html-filename"] = ["recovered.html"],
        });
        TestHostControllerConfiguration controllers = await ((TestHostControllersManager)builder.TestHostControllers).BuildAsync(fixture.Services);
        ITestHostEnvironmentVariableProvider environment = Assert.ContainsSingle(controllers.EnvironmentVariableProviders);
        ITestHostProcessLifetimeHandler lifetime = Assert.ContainsSingle(controllers.LifetimeHandlers);
        var variables = new Mock<IEnvironmentVariables>();
        string? journalPath = null;
        variables.Setup(value => value.SetVariable(It.IsAny<EnvironmentVariable>()))
            .Callback<EnvironmentVariable>(variable => journalPath = variable.Value);
        await environment.UpdateAsync(variables.Object);
        Assert.IsNotNull(journalPath);
        fixture.InputFiles[journalPath] = """
            {"Type":0,"StartTime":"2026-08-31T12:00:00+00:00","ProcessId":42,"FrameworkUid":"original-fw","FrameworkVersion":"3.2.1","FrameworkDisplayName":"Original HTML framework"}
            {"Type":1,"Result":{"Uid":"recovered","DisplayName":"Recovered failure","Outcome":"failed","Duration":"00:00:02","ErrorMessage":"expected 1, got 2"}}
            """;

        await lifetime.OnTestHostProcessExitedAsync(new TestHostProcessInformation(314, 17, false), CancellationToken.None);

        FileArtifact artifact = Assert.IsInstanceOfType<FileArtifact>(Assert.ContainsSingle(fixture.Artifacts));
        Assert.AreEqual("recovered.html", artifact.FileInfo.Name);
        Assert.AreEqual("microsoft.testing.html", artifact.Kind);
        Assert.AreSequenceEqual(new[] { "warning", "artifact" }, fixture.Events);
        Assert.AreSequenceEqual(
            new[] { "The test host terminated before report generation completed. Recovered 1 terminal test result(s); the generated report is marked incomplete." },
            fixture.Warnings);
        JsonNode actual = JsonNode.Parse(HtmlReportEngine.ExtractReportJson(fixture.WrittenFiles[Path.Combine("results", "recovered.html")].Text))!;
        JsonNode expected = JsonNode.Parse("""
            {
              "schemaVersion":"1","generator":"Microsoft.Testing.Extensions.HtmlReport","generatorVersion":"",
              "testApplication":"tests.dll","machineName":"agent","userName":"",
              "framework":"Original HTML framework","frameworkUid":"original-fw","frameworkVersion":"3.2.1",
              "startTime":"2026-08-31T12:00:00.0000000+00:00","endTime":"2026-08-31T12:00:00.0000000+00:00",
              "exitCode":17,"incomplete":true,"runStatus":"aborted",
              "tests":[{"rowKey":0,"uid":"recovered","displayName":"Recovered failure","outcome":"failed","durationMs":2000.000,"errorMessage":"expected 1, got 2"}],
              "summary":{"total":1,"passed":0,"failed":1,"skipped":0,"timedOut":0,"errored":0,"totalDurationMs":0.000}
            }
            """)!;
        expected["generatorVersion"] = lifetime.Version;
        Assert.AreEqual(expected.ToJsonString(), actual.ToJsonString());
        fixture.FileSystem.Verify(value => value.DeleteFile(journalPath), Times.Once);
    }
}
