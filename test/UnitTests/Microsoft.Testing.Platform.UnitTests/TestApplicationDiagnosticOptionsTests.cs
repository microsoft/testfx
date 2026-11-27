// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Drives <c>TestApplication.CreateBuilderAsync</c> in process with the diagnostic options and checks where the
/// diagnostic log ends up and what it says. Process-wide environment variables are saved and restored for every
/// test, and the class is serialized so that no other test can observe them.
/// </summary>
[TestClass]
[DoNotParallelize]
[UnsupportedOSPlatform("browser")]
public sealed class TestApplicationDiagnosticOptionsTests
{
    private static readonly string[] ControlledVariables =
    [
        "TESTINGPLATFORM_DIAGNOSTIC",
        "TESTINGPLATFORM_DIAGNOSTIC_VERBOSITY",
        "TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY",
        "TESTINGPLATFORM_DIAGNOSTIC_FILE_PREFIX",
        "TESTINGPLATFORM_DIAGNOSTIC_SYNCHRONOUS_WRITE",
        "TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER",
        "TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER",
        "DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY",
        "TESTINGPLATFORM_UI_LANGUAGE",
        "DOTNET_CLI_UI_LANGUAGE",
        "VSLANG",
        "PreferredUILang",
    ];

    [TestMethod]
    public async Task Diagnostic_LogStartsWithVersionRuntimeAndProcessInformation()
    {
        string root = CreateRoot();
        try
        {
            (string _, string text) = await BuildAndReadLogAsync(root, ["--diagnostic", "--diagnostic-output-directory", Path.Combine(root, "diag")]);

            Assert.IsTrue(Regex.IsMatch(text, @"Version: \S+"), "The informational version of the platform must be logged.");
            Assert.DoesNotContain("Version attribute not found", text);
            Assert.Contains("Logging mode: asynchronous", text);
            Assert.IsTrue(Regex.IsMatch(text, @"CreateBuilderAsync entry time: \d{2}:\d{2}:\d{2}\.\d{3}\r?\n"), "The entry time must use the HH:mm:ss.fff format.");
            Assert.IsTrue(Regex.IsMatch(text, @"PID: \d+"), "The process id must be logged.");
#if NETCOREAPP
            Assert.Contains($"Runtime information: {RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}", text);
            Assert.Contains($"IsDynamicCodeSupported: {RuntimeFeature.IsDynamicCodeSupported}", text);
#else
            Assert.Contains($"Runtime information: {RuntimeInformation.ProcessArchitecture} - {RuntimeInformation.FrameworkDescription}", text);
            Assert.Contains("IsDynamicCodeSupported: False", text);
#endif
            Assert.Contains($"Runtime location: {typeof(object).Assembly.Location}", text);
            Assert.DoesNotContain("Runtime location not found.", text);
            Assert.Contains("Test module: ", text);
            Assert.Contains("Command line arguments: '", text);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_AtTraceLevel_LogsMachineInformation()
    {
        string root = CreateRoot();
        try
        {
            (string _, string text) = await BuildAndReadLogAsync(root, ["--diagnostic", "--diagnostic-output-directory", Path.Combine(root, "diag")]);

            Assert.Contains($"Machine name: {Environment.MachineName}", text);
            Assert.Contains($"OSVersion: {Environment.OSVersion}", text);
            Assert.Contains($"ProcessorCount: {Environment.ProcessorCount}", text);
            Assert.Contains($"Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}", text);
#if NETCOREAPP
            Assert.Contains($"TotalAvailableMemoryBytes(GB): {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1_000_000_000}", text);
#endif
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task CreateBuilder_WithoutDiagnosticOptionOrEnvironment_CreatesNoDiagnosticFolder()
    {
        string root = CreateRoot();
        try
        {
            await CreateBuilderOnlyAsync(root, []);

            Assert.IsFalse(Directory.Exists(Path.Combine(root, "work", "TestResults")), "No diagnostic log must be created when diagnostics are not requested.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task CreateBuilder_WithDiagnosticAndInvalidCommandLine_CreatesNoDiagnosticFolder()
    {
        string root = CreateRoot();
        try
        {
            await CreateBuilderOnlyAsync(root, ["--diagnostic", "@" + Path.Combine(root, "missing.rsp")]);

            Assert.IsFalse(Directory.Exists(Path.Combine(root, "work", "TestResults")), "A command line with errors must not create a diagnostic log.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task CreateBuilder_WithUiLanguageEnvironmentVariable_FlowsTheCultureToOtherVariablesAndTheDefaultUiCulture()
    {
        string root = CreateRoot();
        try
        {
            string? flowed = await RunControlledAsync(
                root,
                new Dictionary<string, string?> { ["TESTINGPLATFORM_UI_LANGUAGE"] = "fr-FR" },
                async () =>
                {
                    TestApplicationOptions options = new() { EnableTelemetry = false };
                    options.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource = false;
                    _ = await TestApplication.CreateBuilderAsync(["--no-banner"], options);
                    Assert.AreEqual("fr-FR", CultureInfo.DefaultThreadCurrentUICulture?.Name);
                    return Environment.GetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE");
                });

            Assert.AreEqual("fr-FR", flowed);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_WithoutVerbosityOption_LogsAtTraceLevel()
    {
        string root = CreateRoot();
        try
        {
            (string _, string text) = await BuildAndReadLogAsync(root, ["--diagnostic", "--diagnostic-output-directory", Path.Combine(root, "diag")]);

            Assert.Contains("Logging level: Trace", text);
            Assert.Contains("Machine info:", text);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_WithVerbosityOption_UsesTheRequestedLevel()
    {
        string root = CreateRoot();
        try
        {
            (string _, string text) = await BuildAndReadLogAsync(
                root,
                ["--diagnostic", "--diagnostic-verbosity", "information", "--diagnostic-output-directory", Path.Combine(root, "diag")]);

            Assert.Contains("Logging level: Information", text);
            Assert.DoesNotContain("Logging level: Trace", text);
            Assert.DoesNotContain("Machine info:", text);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_WithResultsDirectoryOnly_WritesDirectlyThereAndNeverCreatesTheDefaultFolder()
    {
        string root = CreateRoot();
        try
        {
            string results = Path.Combine(root, "results");
            (string log, string _) = await BuildAndReadLogAsync(root, ["--diagnostic", "--results-directory", results]);

            Assert.AreEqual(Path.GetFullPath(results), Path.GetDirectoryName(log));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "work", "TestResults")), "The default diagnostic folder must not be created when --results-directory is given.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_WithResultsDirectoryEndingWithSeparator_KeepsTheLogInPlace()
    {
        string root = CreateRoot();
        try
        {
            string results = Path.Combine(root, "results") + Path.DirectorySeparatorChar;
            (string log, string _) = await BuildAndReadLogAsync(root, ["--diagnostic", "--results-directory", results]);

            Assert.AreEqual(Path.GetFullPath(results).TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(log));
            Assert.HasCount(1, Directory.GetFiles(results, "*.diag"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_WithOutputDirectoryOption_KeepsTheLogThereEvenWhenTheResultsDirectoryDiffers()
    {
        string root = CreateRoot();
        try
        {
            string diag = Path.Combine(root, "diag");
            string results = Path.Combine(root, "configured-results");
            string configuration = WriteResultDirectoryConfiguration(root, results);
            (string log, string _) = await BuildAndReadLogAsync(root, ["--diagnostic", "--diagnostic-output-directory", diag, "--config-file", configuration]);

            Assert.AreEqual(Path.GetFullPath(diag), Path.GetDirectoryName(log));
            Assert.IsEmpty(Directory.GetFiles(results, "*.diag"), "A custom diagnostic directory must not be relocated into the results directory.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_WithOutputDirectoryEnvironmentVariable_KeepsTheLogThereEvenWhenTheResultsDirectoryDiffers()
    {
        string root = CreateRoot();
        try
        {
            string environmentDirectory = Path.Combine(root, "env-diag");
            string results = Path.Combine(root, "configured-results");
            string configuration = WriteResultDirectoryConfiguration(root, results);
            (string log, string _) = await BuildAndReadLogAsync(
                root,
                ["--diagnostic", "--config-file", configuration],
                new Dictionary<string, string?> { ["TESTINGPLATFORM_DIAGNOSTIC_OUTPUT_DIRECTORY"] = environmentDirectory });

            Assert.AreEqual(Path.GetFullPath(environmentDirectory), Path.GetDirectoryName(log));
            Assert.IsEmpty(Directory.GetFiles(results, "*.diag"), "An environment-provided diagnostic directory must not be relocated into the results directory.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_WithoutAnyCustomDirectory_MovesTheLogToTheResultsDirectoryFromTheConfigurationFile()
    {
        string root = CreateRoot();
        try
        {
            string results = Path.Combine(root, "configured-results");
            string configuration = WriteResultDirectoryConfiguration(root, results);
            (string log, string _) = await BuildAndReadLogAsync(root, ["--diagnostic", "--config-file", configuration]);

            Assert.AreEqual(Path.GetFullPath(results), Path.GetDirectoryName(log));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(root, "work", "TestResults"), "*.diag"), "The log must have been moved out of the default folder.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [TestMethod]
    public async Task Diagnostic_DefaultFilePrefix_IsAssemblyThenTargetFrameworkThenArchitecture()
    {
        string root = CreateRoot();
        try
        {
            (string log, string _) = await BuildAndReadLogAsync(root, ["--diagnostic", "--diagnostic-output-directory", Path.Combine(root, "diag")]);

            string assemblyName = Assembly.GetEntryAssembly()!.GetName().Name!;
            string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            string fileName = Path.GetFileName(log);
            Assert.IsTrue(
                Regex.IsMatch(fileName, "^" + Regex.Escape(assemblyName) + @"_net\d+(\.\d)?_" + architecture + @"_.+\.diag$"),
                $"Unexpected default diagnostic file name '{fileName}'.");
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string WriteResultDirectoryConfiguration(string root, string results)
    {
        string configuration = Path.Combine(root, "testconfig.json");
        File.WriteAllText(configuration, "{\"platformOptions\":{\"resultDirectory\":\"" + results.Replace('\\', '/') + "\"}}");
        return configuration;
    }

    private static string CreateRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "mtp-diag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "work"));
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(string LogFile, string Text)> BuildAndReadLogAsync(string root, string[] args, Dictionary<string, string?>? environment = null)
        => await RunControlledAsync(
            root,
            environment,
            async () =>
            {
                TestApplicationOptions options = new() { EnableTelemetry = false };
                options.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource = false;
                ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(["--no-banner", .. args], options);
                _ = builder.RegisterTestFramework(
                    _ => new TestFrameworkCapabilities(),
                    (_, _) => Mock.Of<ITestFramework>());
                using ITestApplication application = await builder.BuildAsync();

                FileLoggerProvider? provider = ((TestApplication)application).ServiceProvider.GetService<FileLoggerProvider>();
                Assert.IsNotNull(provider);
                await DisposeHelper.DisposeAsync(provider);
                string logFile = provider.FileLogger.FileName;
                return (logFile, File.ReadAllText(logFile));
            });

    private static async Task<T> RunControlledAsync<T>(string root, Dictionary<string, string?>? environment, Func<Task<T>> action)
    {
        Dictionary<string, string?> saved = [];
        foreach (string name in ControlledVariables)
        {
            saved[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        CultureInfo? savedCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_CLI_TEST_COMMAND_WORKING_DIRECTORY", Path.Combine(root, "work"));
            foreach (KeyValuePair<string, string?> pair in environment ?? [])
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            return await action();
        }
        finally
        {
            foreach (KeyValuePair<string, string?> pair in saved)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            CultureInfo.DefaultThreadCurrentUICulture = savedCulture;
        }
    }

    private static async Task CreateBuilderOnlyAsync(string root, string[] args, Dictionary<string, string?>? environment = null)
        => await RunControlledAsync(
            root,
            environment,
            async () =>
            {
                TestApplicationOptions options = new() { EnableTelemetry = false };
                options.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource = false;
                _ = await TestApplication.CreateBuilderAsync(["--no-banner", .. args], options);
                return 0;
            });
}
