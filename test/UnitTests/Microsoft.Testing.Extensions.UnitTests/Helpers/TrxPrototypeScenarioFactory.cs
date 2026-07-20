// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class TrxPrototypeScenario
{
    public required IReadOnlyList<TrxTestResult> Results { get; init; }

    public required TrxDocumentExpectation Expectation { get; init; }
}

internal sealed class TrxScenarioRenderOptions
{
    public bool IsTestHostCrashed { get; init; }

    public string TestHostCrashInfo { get; init; } = string.Empty;

    public int ExitCode { get; init; }

    public bool IncludeExtensionArtifact { get; init; }

    public IReadOnlyList<string> FailingArtifactFileNames { get; init; } = [];
}

internal sealed class TrxRenderedScenario
{
    public required XDocument Document { get; init; }

    public required byte[] Bytes { get; init; }

    public required TrxDocumentExpectation Expectation { get; init; }
}

internal static class TrxPrototypeScenarioFactory
{
    public const string MachineName = "scenario-machine";
    public const string UserName = "scenario-user";
    public const string TestModule = "Scenario.Tests.dll";
    public const string GoodResultArtifactName = "scenario-result.txt";
    public const string BadResultArtifactName = "scenario-bad-result.txt";
    public const string ExtensionArtifactName = "scenario-collector.bin";
    public const string FrameworkUid = "scenario-framework";
    public const string FrameworkVersion = "1.0.0";

    public static readonly Guid RunId = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid TestSettingsId = new("10000000-0000-0000-0000-000000000002");
    public static readonly DateTimeOffset StartTime = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset FinishTime = new(2026, 7, 20, 10, 5, 0, TimeSpan.Zero);
    public static readonly string RunName = $"{UserName}@{MachineName} 2026-07-20 10:05:00.0000000";

    private static readonly string[] TestIds =
    [
        "20000000-0000-0000-0000-000000000001",
        "20000000-0000-0000-0000-000000000002",
        "20000000-0000-0000-0000-000000000003",
        "20000000-0000-0000-0000-000000000004",
        "20000000-0000-0000-0000-000000000001",
    ];

    private static readonly string[] ExecutionIds =
    [
        "30000000-0000-0000-0000-000000000001",
        "30000000-0000-0000-0000-000000000002",
        "30000000-0000-0000-0000-000000000003",
        "30000000-0000-0000-0000-000000000004",
        "30000000-0000-0000-0000-000000000005",
    ];

    public static TrxPrototypeScenario CreateMixedResultsScenario()
    {
        TrxTestResult[] results =
        [
            CreateResult(
                TestIds[0],
                "Scenario.Pass[1]",
                "Scenario.Pass",
                TrxTestOutcome.Passed,
                StartTime.AddSeconds(1),
                StartTime.AddSeconds(2)),
            CreateResult(
                TestIds[1],
                "Scenario.Failure",
                "Scenario.Failure",
                TrxTestOutcome.Failed,
                StartTime.AddSeconds(3),
                StartTime.AddSeconds(5)),
            CreateResult(
                TestIds[2],
                "Scenario.Skipped",
                "Scenario.Skipped",
                TrxTestOutcome.Skipped,
                StartTime.AddSeconds(6),
                StartTime.AddSeconds(6)),
            CreateResult(
                TestIds[3],
                "Scenario.Timeout",
                "Scenario.Timeout",
                TrxTestOutcome.Timeout,
                StartTime.AddSeconds(7),
                StartTime.AddSeconds(17)),
            CreateResult(
                TestIds[4],
                "Scenario.Pass[2]",
                "Scenario.Pass",
                TrxTestOutcome.Passed,
                StartTime.AddSeconds(18),
                StartTime.AddSeconds(20)),
        ];

        return CreateScenario(results, ExecutionIds, summaryOutcome: "Failed");
    }

    public static TrxPrototypeScenario CreateUnicodeMetadataScenario()
    {
        string longDescription =
            "Description é漢😀 e\u0301 مرحبا <xml>&\"'\r\n "
            + new string('x', 560);
        TrxTestResult result = new()
        {
            Uid = "20000000-0000-0000-0000-000000000005",
            DisplayName = "Unicode é漢😀 e\u0301 مرحبا <test>",
            Outcome = TrxTestOutcome.Passed,
            StartTime = StartTime.AddSeconds(1),
            EndTime = StartTime.AddSeconds(3),
            Duration = TimeSpan.FromSeconds(2),
            TrxTestDefinitionName = "Unicode.Metadata.Definition",
            TrxFullyQualifiedTypeName = "Scenario.UnicodeMetadataTests",
            TestMethodIdentifier = new TrxTestMethodIdentifier
            {
                Namespace = "Scenario",
                TypeName = "UnicodeMetadataTests",
                MethodName = "UnicodeMetadata",
            },
            ExceptionMessage = "exception é漢😀 <&> control:\u0001",
            ExceptionStackTrace = "stack e\u0301 مرحبا\r\nline",
            Messages =
            [
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = "stdout é漢😀 <&>\r\n control:\u0001" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardOutput, Message = "stdout second e\u0301 مرحبا" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.StandardError, Message = "stderr 漢" },
                new TrxStreamMessage { Kind = TrxStreamMessageKind.DebugOrTrace, Message = "debug 😀" },
            ],
            Categories = ["fast", "unicode-漢"],
            Metadata =
            [
                new TrxTestMetadata { Key = "Owner", Value = "Zoë <owner>" },
                new TrxTestMetadata { Key = "Description", Value = longDescription },
                new TrxTestMetadata { Key = "Priority", Value = "7" },
                new TrxTestMetadata { Key = "Custom<&>", Value = "value é漢😀 " + new string('v', 520) },
            ],
            FileArtifacts =
            [
                new TrxTestFileArtifact { FullPath = GoodResultArtifactName },
                new TrxTestFileArtifact { FullPath = BadResultArtifactName },
            ],
        };

        return CreateScenario(
            [result],
            ["30000000-0000-0000-0000-000000000006"],
            summaryOutcome: "Completed");
    }

    public static async Task<TrxRenderedScenario> RenderAsync(
        TrxPrototypeScenario scenario,
        TrxScenarioRenderOptions? options = null)
    {
        options ??= new TrxScenarioRenderOptions();
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"trx-phase-zero-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            Mock<IFileSystem> fileSystem = new();
            CapturingFileStream stream = new();
            List<(string Source, string Destination)> copiedFiles = [];
            _ = fileSystem.Setup(system => system.ExistFile(It.IsAny<string>())).Returns(false);
            _ = fileSystem.Setup(system => system.NewFileStream(It.IsAny<string>(), FileMode.Create)).Returns(stream);
            _ = fileSystem
                .Setup(system => system.CopyFile(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, bool>((source, destination, _) =>
                {
                    if (options.FailingArtifactFileNames.Contains(Path.GetFileName(source), StringComparer.Ordinal))
                    {
                        throw new UnauthorizedAccessException("Deterministic phase-zero copy failure.");
                    }

                    copiedFiles.Add((source, destination));
                });

            Mock<ITestApplicationModuleInfo> module = new();
            _ = module.Setup(info => info.GetCurrentTestApplicationFullPath()).Returns(TestModule);

            Mock<IEnvironment> environment = new();
            _ = environment.SetupGet(value => value.MachineName).Returns(MachineName);
            _ = environment.SetupGet(value => value.ProcessId).Returns(4242);
            _ = environment
                .Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_TRX_TESTRUN_ID"))
                .Returns(RunId.ToString());
            _ = environment.Setup(value => value.GetEnvironmentVariable("UserName")).Returns(UserName);

            Mock<ICommandLineOptions> commandLine = new();
            Mock<IConfiguration> configuration = new();
            _ = configuration.SetupGet(value => value[It.IsAny<string>()]).Returns(temporaryDirectory);

            Mock<IClock> clock = new();
            _ = clock.SetupGet(value => value.UtcNow).Returns(FinishTime);

            Mock<ITestFramework> testFramework = new();
            _ = testFramework.SetupGet(value => value.Uid).Returns(FrameworkUid);
            _ = testFramework.SetupGet(value => value.Version).Returns(FrameworkVersion);
            _ = testFramework.SetupGet(value => value.DisplayName).Returns("Scenario framework");

            Dictionary<IExtension, List<SessionFileArtifact>> extensionArtifacts = [];
            if (options.IncludeExtensionArtifact)
            {
                extensionArtifacts.Add(
                    new TestExtension(),
                    [new SessionFileArtifact(new SessionUid("phase-zero"), new FileInfo(ExtensionArtifactName), "collector")]);
            }

            TrxReportEngine engine = new(
                fileSystem.Object,
                module.Object,
                environment.Object,
                commandLine.Object,
                configuration.Object,
                clock.Object,
                extensionArtifacts,
                testFramework.Object,
                StartTime,
#if NETCOREAPP
                options.ExitCode,
                CancellationToken.None);
#else
                options.ExitCode);
#endif

            _ = await engine.GenerateReportAsync(
                scenario.Results,
                options.TestHostCrashInfo,
                options.IsTestHostCrashed);

            byte[] renderedBytes = stream.ToArray();
            XDocument rendered = LoadStrict(renderedBytes);
            NormalizeGeneratedIds(rendered, scenario.Expectation);
            byte[] normalizedBytes = SaveUtf8(rendered);
            return new TrxRenderedScenario
            {
                Document = LoadStrict(normalizedBytes),
                Bytes = normalizedBytes,
                Expectation = scenario.Expectation,
            };
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    public static byte[] SaveUtf8(XDocument document)
    {
        using MemoryStream stream = new();
        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            Indent = true,
            OmitXmlDeclaration = false,
        };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return stream.ToArray();
    }

    public static XDocument LoadStrict(byte[] bytes)
    {
        string text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text.Substring(1);
        }

        return XDocument.Parse(text, LoadOptions.PreserveWhitespace);
    }

    private static TrxPrototypeScenario CreateScenario(
        IReadOnlyList<TrxTestResult> results,
        IReadOnlyList<string> executionIds,
        string summaryOutcome)
    {
        var expected = new TrxExpectedResult[results.Count];
        for (int i = 0; i < results.Count; i++)
        {
            expected[i] = TrxExpectedResultFactory.Create(
                results[i],
                Guid.Parse(executionIds[i]),
                MachineName,
                TestModule,
                FrameworkUid,
                FrameworkVersion,
                FinishTime);
        }

        return new TrxPrototypeScenario
        {
            Results = results,
            Expectation = new TrxDocumentExpectation
            {
                RunId = RunId,
                RunName = RunName,
                StartTime = StartTime,
                FinishTime = FinishTime,
                SummaryOutcome = summaryOutcome,
                CompletedResults = expected,
                SummaryChildOrder = ["Counters", "CollectorDataEntries", "RunInfos"],
            },
        };
    }

    private static TrxTestResult CreateResult(
        string uid,
        string displayName,
        string definitionName,
        TrxTestOutcome outcome,
        DateTimeOffset start,
        DateTimeOffset end)
        => new()
        {
            Uid = uid,
            DisplayName = displayName,
            Outcome = outcome,
            StartTime = start,
            EndTime = end,
            Duration = end - start,
            TrxTestDefinitionName = definitionName,
            TrxFullyQualifiedTypeName = "Scenario.ContractTests",
            TestMethodIdentifier = new TrxTestMethodIdentifier
            {
                Namespace = "Scenario",
                TypeName = "ContractTests",
                MethodName = definitionName,
            },
        };

    private static void NormalizeGeneratedIds(XDocument document, TrxDocumentExpectation expectation)
    {
        XNamespace ns = TrxDocumentClassifier.TeamTest2010Namespace;
        XElement root = document.Root!;
        XElement? testSettings = root.Element(ns + "TestSettings");
        testSettings?.SetAttributeValue("id", TestSettingsId);

        List<XElement> results = [.. root.Element(ns + "Results")!.Elements(ns + "UnitTestResult")];
        Dictionary<string, string> replacements = [];
        for (int i = 0; i < Math.Min(results.Count, expectation.CompletedResults.Count); i++)
        {
            string actual = results[i].Attribute("executionId")!.Value;
            string expected = expectation.CompletedResults[i].ExecutionId;
            replacements[actual] = expected;
            results[i].SetAttributeValue("executionId", expected);
            results[i].SetAttributeValue("relativeResultsDirectory", expected);
        }

        foreach (XAttribute executionId in document
                     .Descendants()
                     .Attributes("executionId")
                     .Where(attribute => replacements.ContainsKey(attribute.Value)))
        {
            executionId.Value = replacements[executionId.Value];
        }

        foreach (XAttribute definitionExecutionId in document
                     .Descendants(ns + "Execution")
                     .Attributes("id")
                     .Where(attribute => replacements.ContainsKey(attribute.Value)))
        {
            definitionExecutionId.Value = replacements[definitionExecutionId.Value];
        }
    }

    private sealed class CapturingFileStream : IFileStream
    {
        private readonly MemoryStream _stream = new();

        Stream IFileStream.Stream => _stream;

        string IFileStream.Name => string.Empty;

        public byte[] ToArray() => _stream.ToArray();

        public void Dispose()
        {
        }

#if NETCOREAPP
        public ValueTask DisposeAsync() => default;
#endif
    }
}
