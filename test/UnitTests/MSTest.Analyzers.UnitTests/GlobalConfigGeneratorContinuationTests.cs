// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class GlobalConfigGeneratorContinuationTests
{
    [TestMethod]
    public async Task MissingOutputPathFailsBeforeProducingFiles()
    {
        using var directory = new OwnedDirectory();

        ToolResult result = await RunToolAsync(directory.Path, provideOutputPath: false);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "System.InvalidOperationException: OUTPUT_PATH environment variable is not set.");
        Assert.IsEmpty(Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task ActualAnalyzerAssemblyProducesAllFourModeConfigurations()
    {
#if DEBUG
        const string expectedConfiguration = "Debug";
#else
        const string expectedConfiguration = "Release";
#endif
        Assert.AreEqual(expectedConfiguration, Directory.GetParent(System.IO.Path.GetDirectoryName(GetToolPath())!)!.Name);
        using var directory = new OwnedDirectory();
        File.Copy(typeof(UseParallelizeAttributeAnalyzer).Assembly.Location, System.IO.Path.Combine(directory.Path, "MSTest.Analyzers.dll"));

        ToolResult result = await RunToolAsync(directory.Path);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertModeFiles(directory.Path);
        foreach ((string mode, string severity) in new[]
        {
            ("none", "none"),
            ("default", "suggestion"),
            ("recommended", "warning"),
            ("all", "warning"),
        })
        {
            string text = ReadMode(directory.Path, mode);
            StringAssert.Contains(text, $"dotnet_diagnostic.MSTEST0001.severity = {severity}{Environment.NewLine}");
            StringAssert.Contains(text, $"dotnet_diagnostic.MSTEST0059.severity = {(mode == "none" ? "none" : "warning")}{Environment.NewLine}");
            string[] identifiers = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("dotnet_diagnostic.", StringComparison.Ordinal))
                .Select(line => line.Substring("dotnet_diagnostic.".Length, "MSTEST0001".Length))
                .ToArray();
            CollectionAssert.AreEqual(identifiers.OrderBy(id => id, StringComparer.Ordinal).ToArray(), identifiers);
            Assert.AreEqual(identifiers.Length, identifiers.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [TestMethod]
    public async Task FixtureRulesProduceOrderedModeSpecificSeverities()
    {
        using var directory = new OwnedDirectory();
        await EmitAnalyzerAsync(directory.Path, """
            new DiagnosticDescriptor("COV0007", "Escalated rule", "Message", "Contract", DiagnosticSeverity.Warning, true, customTags: new[] { "EscalateToErrorInRecommended" }),
            new DiagnosticDescriptor("COV0006", "Opt-in rule", "Message", "Contract", DiagnosticSeverity.Info, false, customTags: new[] { "DisableInAllMode" }),
            new DiagnosticDescriptor("COV0005", "Hidden rule", "Message", "Contract", DiagnosticSeverity.Hidden, true),
            new DiagnosticDescriptor("COV0004", "Error rule", "Message", "Contract", DiagnosticSeverity.Error, true),
            new DiagnosticDescriptor("COV0003", "Disabled rule", "Message", "Contract", DiagnosticSeverity.Info, false),
            new DiagnosticDescriptor("COV0002", "Info rule", "Message", "Contract", DiagnosticSeverity.Info, true),
            new DiagnosticDescriptor("COV0002", "Info rule", "Message", "Contract", DiagnosticSeverity.Info, true),
            new DiagnosticDescriptor("COV0001", "Warning rule", "Message", "Contract", DiagnosticSeverity.Warning, true)
            """);

        ToolResult result = await RunToolAsync(directory.Path);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertModeFiles(directory.Path);
        AssertMode(
            directory.Path, "None",
            ["none", "none", "none", "error", "none", "none", "none"]);
        AssertMode(
            directory.Path, "Default",
            ["warning", "suggestion", "none", "error", "silent", "none", "warning"]);
        AssertMode(
            directory.Path, "Recommended",
            ["warning", "warning", "none", "error", "suggestion", "none", "error"]);
        AssertMode(
            directory.Path, "All",
            ["warning", "warning", "warning", "error", "warning", "none", "error"]);
    }

    [TestMethod]
    [DataRow("DisabledWarning", "DiagnosticSeverity.Warning, false", "", "Rules with severity >= Warning are expected to be enabled by default.", DisplayName = "DisabledWarning")]
    [DataRow("EnabledOptIn", "DiagnosticSeverity.Info, true", "DisableInAllMode", "Rules with DisableInAllMode custom tag are expected to be disabled by default and have severity Info.", DisplayName = "EnabledOptIn")]
    [DataRow("InfoEscalation", "DiagnosticSeverity.Info, false", "EscalateToErrorInRecommended", "Is it intended that default severity is not warning when escalating to error in recommended mode?", DisplayName = "InfoEscalation")]
    [DataRow("InvalidSeverity", "(DiagnosticSeverity)23, true", "", "Unexpected severity '23'.", DisplayName = "InvalidSeverity")]
    public async Task InvalidRulePolicyFailsWithoutPartialConfigurations(
        string policy,
        string severityAndEnablement,
        string tag,
        string expectedMessage)
    {
        using var directory = new OwnedDirectory();
        await EmitAnalyzerAsync(directory.Path, $$"""
            new DiagnosticDescriptor("COV0001", "{{policy}}", "Message", "Contract", {{severityAndEnablement}}, customTags: new[] { "{{tag}}" })
            """);

        ToolResult result = await RunToolAsync(directory.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        StringAssert.Contains(result.StandardError, $"System.InvalidOperationException: {expectedMessage}");
        Assert.IsFalse(Directory.Exists(System.IO.Path.Combine(directory.Path, "globalconfigs")));
    }

    [TestMethod]
    public async Task ConflictingDuplicateRuleFailsWithoutPartialConfigurations()
    {
        using var directory = new OwnedDirectory();
        await EmitAnalyzerAsync(directory.Path, """
            new DiagnosticDescriptor("COV0001", "First", "Message", "Contract", DiagnosticSeverity.Warning, true),
            new DiagnosticDescriptor("COV0001", "Conflicting", "Message", "Contract", DiagnosticSeverity.Info, true)
            """);

        ToolResult result = await RunToolAsync(directory.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "System.InvalidOperationException: Rule 'COV0001' has conflicting severities 'Warning' and 'Info'.");
        Assert.IsFalse(Directory.Exists(System.IO.Path.Combine(directory.Path, "globalconfigs")));
    }

    [TestMethod]
    public async Task MissingAnalyzerAssemblyFailsWithoutPartialConfigurations()
    {
        using var directory = new OwnedDirectory();

        ToolResult result = await RunToolAsync(directory.Path);

        Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
        StringAssert.Contains(result.StandardError, "System.IO.FileNotFoundException");
        StringAssert.Contains(result.StandardError, "MSTest.Analyzers.dll");
        Assert.IsEmpty(Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task AssemblyWithoutAnalyzersProducesFourEmptyConfigurations()
    {
        using var directory = new OwnedDirectory();
        await EmitAssemblyAsync(directory.Path, "public sealed class NotAnAnalyzer { }");

        ToolResult result = await RunToolAsync(directory.Path);

        Assert.AreEqual(0, result.ExitCode, result.StandardError);
        AssertModeFiles(directory.Path);
        foreach (string mode in new[] { "None", "Default", "Recommended", "All" })
        {
            string expected = $"# A GlobalConfig file for the '{mode}' MSTest analysis mode.{Environment.NewLine}{Environment.NewLine}"
                + $"is_global = true{Environment.NewLine}global_level = -100{Environment.NewLine}{Environment.NewLine}";
            Assert.AreEqual(expected, ReadMode(directory.Path, mode.ToLowerInvariant()));
        }
    }

    private static void AssertModeFiles(string directory)
        => CollectionAssert.AreEquivalent(
            new[]
            {
                "mstest-none.globalconfig",
                "mstest-default.globalconfig",
                "mstest-recommended.globalconfig",
                "mstest-all.globalconfig",
            },
            Directory.GetFiles(System.IO.Path.Combine(directory, "globalconfigs"))
                .Select(System.IO.Path.GetFileName)
                .ToArray());

    private static void AssertMode(string directory, string mode, string[] severities)
    {
        string[] titles = ["Warning rule", "Info rule", "Disabled rule", "Error rule", "Hidden rule", "Opt-in rule", "Escalated rule"];
        var expected = new StringBuilder();
        expected.AppendLine("# A GlobalConfig file for the '" + mode + "' MSTest analysis mode.");
        expected.AppendLine();
        expected.AppendLine("is_global = true");
        expected.AppendLine("global_level = -100");
        expected.AppendLine();
        for (int index = 0; index < titles.Length; index++)
        {
            string id = "COV" + (index + 1).ToString("0000", CultureInfo.InvariantCulture);
            expected.AppendLine("# " + id + ": " + titles[index]);
            expected.AppendLine("dotnet_diagnostic." + id + ".severity = " + severities[index]);
            expected.AppendLine();
        }

        Assert.AreEqual(expected.ToString(), ReadMode(directory, mode.ToLowerInvariant()));
    }

    private static string ReadMode(string directory, string mode)
        => File.ReadAllText(System.IO.Path.Combine(directory, "globalconfigs", $"mstest-{mode}.globalconfig"));

    private static Task EmitAnalyzerAsync(string directory, string descriptors)
        => EmitAssemblyAsync(directory, $$"""
            using System.Collections.Immutable;
            using Microsoft.CodeAnalysis;
            using Microsoft.CodeAnalysis.Diagnostics;
            [DiagnosticAnalyzer(LanguageNames.CSharp)]
            public sealed class ContractAnalyzer : DiagnosticAnalyzer
            {
                public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
                    = ImmutableArray.Create<DiagnosticDescriptor>(new DiagnosticDescriptor[] { {{descriptors}} });
                public override void Initialize(AnalysisContext context) { }
            }
            """);

    private static async Task EmitAssemblyAsync(string directory, string source)
    {
        ImmutableArray<MetadataReference> references = await AnalyzerTestReferenceAssemblies.Default.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        references = references.Add(MetadataReference.CreateFromFile(
            System.IO.Path.Combine(System.IO.Path.GetDirectoryName(GetToolPath())!, "Microsoft.CodeAnalysis.dll")));
        if (!references.Any(reference => System.IO.Path.GetFileName(reference.Display) == "System.Collections.Immutable.dll"))
        {
            references = references.Add(MetadataReference.CreateFromFile(typeof(ImmutableArray<>).Assembly.Location));
        }

        var compilation = CSharpCompilation.Create(
            "MSTest.Analyzers",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        EmitResult emitted = compilation.Emit(System.IO.Path.Combine(directory, "MSTest.Analyzers.dll"));
        Assert.IsTrue(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
    }

    private static async Task<ToolResult> RunToolAsync(string directory, bool provideOutputPath = true)
    {
        string root = RootFinder.Find();
        string tool = GetToolPath();
        Assert.IsTrue(File.Exists(tool), $"The unchanged tool output is required; this test must not build production references: {tool}");
        string host = System.IO.Path.Combine(root, ".dotnet", Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet");
        var startInfo = new ProcessStartInfo(host, $"\"{tool}\"")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (provideOutputPath)
        {
            startInfo.EnvironmentVariables["OUTPUT_PATH"] = directory;
        }
        else
        {
            startInfo.EnvironmentVariables.Remove("OUTPUT_PATH");
        }

        using var process = new Process { StartInfo = startInfo };
        Assert.IsTrue(process.Start());
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try
        {
            await Task.Run(process.WaitForExit);
            return new ToolResult(process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
    }

    private sealed record ToolResult(int ExitCode, string StandardOutput, string StandardError);

    private static string GetToolPath()
    {
        string configuration = typeof(GlobalConfigGeneratorContinuationTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        return System.IO.Path.Combine(RootFinder.Find(), "artifacts", "bin", "MSTest.GlobalConfigsGenerator", configuration, "net12.0", "MSTest.GlobalConfigsGenerator.dll");
    }

    private sealed class OwnedDirectory : IDisposable
    {
        internal OwnedDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gcc" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
