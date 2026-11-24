// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public class ReportExceptionDiagnosticsTests : AcceptanceTestBase<NopAssetFixture>
{
    [TestMethod]
    public async Task Reports_NestedExceptions_PreserveAssociatedCausesAndExplanations()
    {
        using TempDirectory directory = new();
        using TestAsset asset = await TestAsset.GenerateAssetAsync(
            AssetName,
            TestCode
                .PatchTargetFrameworks([TargetFrameworks.NetCurrent])
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsCtrfReportVersion$", MicrosoftTestingExtensionsCtrfReportVersion)
                .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsJUnitReportVersion$", MicrosoftTestingExtensionsJUnitReportVersion),
            directory);
        await DotnetCli.RunAsync(
            $"build \"{asset.TargetAssetPath}\" -c Release",
            cancellationToken: TestContext.CancellationToken);

        string resultsDirectory = Path.Combine(directory.Path, "results");
        var testHost = TestInfrastructure.TestHost.LocateFrom(asset.TargetAssetPath, AssetName, TargetFrameworks.NetCurrent);
        TestHostResult result = await testHost.ExecuteAsync(
            $"--report-ctrf --report-ctrf-filename diagnostics.ctrf.json"
            + " --report-html --report-html-filename diagnostics.html"
            + " --report-junit --report-junit-filename diagnostics.xml"
            + $" --results-directory \"{resultsDirectory}\"",
            cancellationToken: TestContext.CancellationToken);
        result.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        using var ctrf = JsonDocument.Parse(File.ReadAllText(Path.Combine(resultsDirectory, "diagnostics.ctrf.json")));
        JsonElement[] ctrfTests = [.. ctrf.RootElement.GetProperty("results").GetProperty("tests").EnumerateArray()];
        Assert.HasCount(5, ctrfTests);
        Dictionary<string, JsonElement> ctrfById = ctrfTests.ToDictionary(test => test.GetProperty("testId").GetString()!, StringComparer.Ordinal);

        string html = File.ReadAllText(Path.Combine(resultsDirectory, "diagnostics.html"));
        Match payload = Regex.Match(html, """<script id="mtp-data" type="application/json">(?<data>.*?)</script>""", RegexOptions.Singleline);
        Assert.IsTrue(payload.Success, "The HTML report must contain its consumed result data.");
        using var htmlData = JsonDocument.Parse(payload.Groups["data"].Value);
        JsonElement[] htmlTests = [.. htmlData.RootElement.GetProperty("tests").EnumerateArray()];
        Assert.HasCount(5, htmlTests);
        Dictionary<string, JsonElement> htmlById = htmlTests.ToDictionary(test => test.GetProperty("uid").GetString()!, StringComparer.Ordinal);

        var junit = XDocument.Load(Path.Combine(resultsDirectory, "diagnostics.xml"));
        XElement[] junitTests = [.. junit.Descendants("testcase")];
        Assert.HasCount(5, junitTests);
        Dictionary<string, XElement> junitById = junitTests.ToDictionary(
            test => test.Element("properties")!.Elements("property").Single(property => (string?)property.Attribute("name") == "uid").Attribute("value")!.Value,
            StringComparer.Ordinal);

        AssertFailure("nested", "NestedFailure", "Invocation failed.", "System.InvalidOperationException", "failed",
            ["System.ArgumentException: [INNER_CAUSE] Service rejected <401> & \"credential\"."]);
        AssertFailure("aggregate", "AggregateFailure", "Parallel failure. (Read failed.) ([AUTH_CAUSE] Credential rejected.)", "System.AggregateException", "errored",
            ["System.IO.IOException: [READ_CAUSE] Missing file.", "System.ArgumentException: [AUTH_CAUSE] Credential rejected."]);
        AssertFailure("explained", "ExplainedFailure", "Framework explanation.", "System.InvalidOperationException", "failed",
            ["System.IO.IOException: [EXPLAINED_CAUSE] Missing file."]);
        AssertFailure("simple", "SimpleFailure", "Simple failure.", "System.InvalidOperationException", "failed", []);

        Assert.AreEqual("PassingTest", ctrfById["passed"].GetProperty("name").GetString());
        Assert.AreEqual("passed", ctrfById["passed"].GetProperty("status").GetString());
        Assert.AreEqual("PassingTest", htmlById["passed"].GetProperty("displayName").GetString());
        Assert.AreEqual("passed", htmlById["passed"].GetProperty("outcome").GetString());
        Assert.AreEqual("PassingTest", (string?)junitById["passed"].Attribute("name"));
        Assert.IsNull(junitById["passed"].Element("failure"));
        Assert.IsNull(junitById["passed"].Element("error"));

        void AssertFailure(string uid, string name, string message, string exceptionType, string htmlOutcome, string[] causes)
        {
            JsonElement ctrfTest = ctrfById[uid];
            Assert.AreEqual(name, ctrfTest.GetProperty("name").GetString());
            Assert.AreEqual("failed", ctrfTest.GetProperty("status").GetString());
            Assert.AreEqual(message, ctrfTest.GetProperty("message").GetString());

            JsonElement htmlTest = htmlById[uid];
            Assert.AreEqual(name, htmlTest.GetProperty("displayName").GetString());
            Assert.AreEqual(htmlOutcome, htmlTest.GetProperty("outcome").GetString());
            Assert.AreEqual(message, htmlTest.GetProperty("errorMessage").GetString());
            Assert.AreEqual(exceptionType, htmlTest.GetProperty("exceptionType").GetString());

            XElement junitTest = junitById[uid];
            Assert.AreEqual(name, (string?)junitTest.Attribute("name"));
            XElement? failure = junitTest.Element(htmlOutcome == "errored" ? "error" : "failure");
            Assert.IsNotNull(failure);
            Assert.AreEqual(message, (string?)failure.Attribute("message"));
            Assert.AreEqual(exceptionType, (string?)failure.Attribute("type"));

            if (causes.Length == 0)
            {
                Assert.IsFalse(ctrfTest.TryGetProperty("trace", out _));
                Assert.IsFalse(htmlTest.TryGetProperty("stackTrace", out _));
                Assert.AreEqual($"{exceptionType}: {message}", failure.Value);
                return;
            }

            Assert.IsTrue(ctrfTest.TryGetProperty("trace", out JsonElement ctrfTrace), $"CTRF result '{uid}' must retain its nested exception trace.");
            Assert.IsTrue(htmlTest.TryGetProperty("stackTrace", out JsonElement htmlTrace), $"HTML result '{uid}' must retain its nested exception trace.");
            foreach (string cause in causes)
            {
                Assert.Contains(cause, ctrfTrace.GetString()!, $"CTRF result '{uid}' lost a cause.");
                Assert.Contains(cause, htmlTrace.GetString()!, $"HTML result '{uid}' lost a cause.");
                Assert.Contains(cause, failure.Value, $"JUnit result '{uid}' lost a cause.");
            }
        }
    }

    private const string AssetName = "ReportExceptionDiagnostics";
    private const string TestCode = """
#file ReportExceptionDiagnostics.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Testing.Extensions.CtrfReport" Version="$MicrosoftTestingExtensionsCtrfReportVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.HtmlReport" Version="$MicrosoftTestingPlatformVersion$" />
    <PackageReference Include="Microsoft.Testing.Extensions.JUnitReport" Version="$MicrosoftTestingExtensionsJUnitReportVersion$" />
  </ItemGroup>
</Project>

#file Program.cs
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;

ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, _) => new DiagnosticFramework());
builder.AddCtrfReportProvider();
#pragma warning disable TPEXP
builder.AddHtmlReportProvider();
builder.AddJUnitReportProvider();
#pragma warning restore TPEXP
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

public sealed class DiagnosticFramework : ITestFramework, IDataProducer
{
    public string Uid => nameof(DiagnosticFramework);
    public string Version => "1.0.0";
    public string DisplayName => nameof(DiagnosticFramework);
    public string Description => nameof(DiagnosticFramework);
    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
        => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
        => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        await PublishAsync("nested", "NestedFailure", new FailedTestNodeStateProperty(
            new InvalidOperationException("Invocation failed.", new ArgumentException("[INNER_CAUSE] Service rejected <401> & \"credential\"."))));
        await PublishAsync("aggregate", "AggregateFailure", new ErrorTestNodeStateProperty(
            new AggregateException("Parallel failure.",
                new InvalidOperationException("Read failed.", new IOException("[READ_CAUSE] Missing file.")),
                new ArgumentException("[AUTH_CAUSE] Credential rejected."))));
        await PublishAsync("explained", "ExplainedFailure", new FailedTestNodeStateProperty(
            new InvalidOperationException("Outer message.", new IOException("[EXPLAINED_CAUSE] Missing file.")), "Framework explanation."));
        await PublishAsync("simple", "SimpleFailure", new FailedTestNodeStateProperty(new InvalidOperationException("Simple failure.")));
        await PublishAsync("passed", "PassingTest", PassedTestNodeStateProperty.CachedInstance);
        context.Complete();

        Task PublishAsync(string uid, string displayName, TestNodeStateProperty state)
            => context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(
                context.Request.Session.SessionUid,
                new TestNode { Uid = uid, DisplayName = displayName, Properties = new PropertyBag(state) }));
    }
}
""";

    public TestContext TestContext { get; set; }
}
