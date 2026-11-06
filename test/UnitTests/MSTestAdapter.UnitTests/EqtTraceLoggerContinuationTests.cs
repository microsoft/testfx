// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.VSTestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class EqtTraceLoggerContinuationTests : TestContainer
{
    private const string ChildDirectoryVariable = "TESTFX_COVERAGE_EQT_CHILD_DIRECTORY";

    public async Task TraceBridgePersistsEverySeverityInAnIsolatedHost()
    {
        string? childDirectory = Environment.GetEnvironmentVariable(ChildDirectoryVariable);
        if (childDirectory is not null)
        {
            Environment.GetEnvironmentVariable("MTP_AFFECTED_TESTS_BATCH_FEEDBACK_PATH").Should().BeNull(
                "the isolated host must not overwrite its parent's collection-batch feedback");
            string requestedLog = Path.Combine(childDirectory, "eqt.log");
            EqtTrace.DoNotInitailize = false;
            EqtTrace.InitializeTrace(requestedLog, PlatformTraceLevel.Verbose).Should().BeTrue();
            ITraceLogger logger = EqtTraceLogger.Instance;
            logger.IsVerboseEnabled.Should().BeTrue();
            logger.IsInfoEnabled.Should().BeTrue();
            logger.IsWarningEnabled.Should().BeTrue();
            logger.IsErrorEnabled.Should().BeTrue();
            logger.Verbose("eqt-trace-single");
            logger.Verbose("eqt-trace-format {0}", 17);
            logger.Info("eqt-info-single");
            logger.Info("eqt-info-format {0}", 23);
            logger.Warning("eqt-warning-single");
            logger.Warning("eqt-warning-format {0}", 31);
            logger.Error("eqt-error-single");
            logger.Error("eqt-error-format {0}", 42);
            logger.Error(new InvalidOperationException("eqt-exception-marker"));
#if NETFRAMEWORK
            var owned = AppDomain.CreateDomain("eqt-bridge-owned-domain");
            try
            {
                logger.SetupRemoteEqtTraceListeners(owned);
            }
            finally
            {
                AppDomain.Unload(owned);
            }
#endif
            File.WriteAllText(Path.Combine(childDirectory, "trace-path.txt"), EqtTrace.LogFile);
            return;
        }

        string assemblyPath = typeof(EqtTraceLoggerContinuationTests).Assembly.Location;
        string[] commandLine = Environment.GetCommandLineArgs();
        int resultsIndex = Array.IndexOf(commandLine, "--results-directory");
        string? resultsDirectory = resultsIndex >= 0
            ? commandLine[resultsIndex + 1]
            : Environment.GetEnvironmentVariable("TEST_UNDECLARED_OUTPUTS_DIR");
        resultsDirectory.Should().NotBeNullOrEmpty("the test host must supply an isolated result directory");
        string childResults = Path.GetFullPath(Path.Combine(resultsDirectory!, "eqt-child-" + Guid.NewGuid().ToString("N")));
        Directory.Exists(childResults).Should().BeFalse("each child invocation owns an isolated result directory");
        Directory.CreateDirectory(childResults);
#if NETCOREAPP
        string host = Environment.ProcessPath!;
#else
        string host = assemblyPath;
#endif
        var startInfo = new ProcessStartInfo(host)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
#if NETCOREAPP
        if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(assemblyPath);
        }

        foreach (string argument in new[] { "--results-directory", childResults, "--report-trx", "--report-trx-filename", "child.trx", "--treenode-filter", "/*/*/EqtTraceLoggerContinuationTests/*" })
        {
            startInfo.ArgumentList.Add(argument);
        }
#else
        startInfo.Arguments = $"--results-directory \"{childResults}\" --report-trx --report-trx-filename child.trx --treenode-filter \"/*/*/EqtTraceLoggerContinuationTests/*\"";
#endif
        startInfo.EnvironmentVariables[ChildDirectoryVariable] = childResults;
        // The nested SDK invocation must not join its parent's affected-test collection session.
        startInfo.EnvironmentVariables.Remove("DOTNET_CLI_ENABLE_AFFECTED_TESTS");
        startInfo.EnvironmentVariables.Remove("DOTNET_CLI_TEST_AFFECTED_TESTS_MODE");
        startInfo.EnvironmentVariables.Remove("MTP_AFFECTED_TESTS_CHILD_PROCESS");
        startInfo.EnvironmentVariables.Remove("MTP_AFFECTED_TESTS_BATCH_FEEDBACK_ID");
        startInfo.EnvironmentVariables.Remove("MTP_AFFECTED_TESTS_BATCH_FEEDBACK_PATH");
        using Process child = Process.Start(startInfo)!;
        Task<string> standardOutput = child.StandardOutput.ReadToEndAsync();
        Task<string> standardError = child.StandardError.ReadToEndAsync();
        await Task.Run(child.WaitForExit);
        string output = await standardOutput;
        string error = await standardError;
#if NETCOREAPP
        string arguments = string.Join(" ", startInfo.ArgumentList.Select(argument => $"\"{argument}\""));
#else
        string arguments = startInfo.Arguments;
#endif
        File.WriteAllText(Path.Combine(childResults, "child-command.txt"), host + " " + arguments);
        File.WriteAllText(Path.Combine(childResults, "child-console.txt"), output + Environment.NewLine + error);
        child.ExitCode.Should().Be(0, "the isolated owned test host must pass, output: {0}", output + error);

        string trxPath = Directory.GetFiles(childResults, "*.trx", SearchOption.AllDirectories).Single();
        var trx = XDocument.Load(trxPath);
        XElement counters = trx.Descendants().Single(element => element.Name.LocalName == "Counters");
        counters.Attribute("total")!.Value.Should().Be("1");
        counters.Attribute("passed")!.Value.Should().Be("1");
        counters.Attribute("failed")!.Value.Should().Be("0");
        trx.Descendants().Where(element => element.Name.LocalName == "UnitTestResult").Should().ContainSingle()
            .Which.Attribute("outcome")!.Value.Should().Be("Passed");
        string actualLog = File.ReadAllText(Path.Combine(childResults, "trace-path.txt"));
        string trace = File.ReadAllText(actualLog);
        string[] messages =
        [
            "eqt-trace-single", "eqt-trace-format 17", "eqt-info-single", "eqt-info-format 23",
            "eqt-warning-single", "eqt-warning-format 31", "eqt-error-single", "eqt-error-format 42", "eqt-exception-marker",
        ];
        int previous = -1;
        foreach (string message in messages)
        {
            int position = trace.IndexOf(message, StringComparison.Ordinal);
            position.Should().BeGreaterThan(previous, "the trace bridge must persist each message in call order");
            previous = position;
        }
    }
}
