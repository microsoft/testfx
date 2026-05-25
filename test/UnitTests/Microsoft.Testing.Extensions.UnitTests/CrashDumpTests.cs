// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics;
using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CrashDumpTests
{
    [TestMethod]
    [DataRow("Mini")]
    [DataRow("Heap")]
    [DataRow("Triage")]
    [DataRow("Full")]
    public async Task IsValid_If_CrashDumpType_Has_CorrectValue(string crashDumpType)
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [crashDumpType]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvValid_If_CrashDumpType_Has_IncorrectValue()
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashDumpTypeOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["invalid"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpTypeOptionInvalidType, "invalid"), validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    public async Task CrashDump_CommandLineOptions_Are_Valid_ByDefault()
    {
        var provider = new CrashDumpCommandLineProvider();

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions([])).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpTypeOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashSequenceOptionName)]
    public async Task Missing_CrashDumpMainOption_ShouldReturn_IsInvalid(string crashDumpArgument)
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { crashDumpArgument, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(CrashDumpResources.MissingCrashDumpMainOption, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpTypeOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashSequenceOptionName)]
    public async Task If_CrashDumpMainOption_IsSpecified_ShouldReturn_IsValid(string crashDumpArgument)
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { crashDumpArgument, [] },
            { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [DataRow("on")]
    [DataRow("off")]
    [DataRow("true")]
    [DataRow("false")]
    [DataRow("enable")]
    [DataRow("disable")]
    [DataRow("1")]
    [DataRow("0")]
    public async Task IsValid_If_CrashSequence_Has_CorrectValue(string value)
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashSequenceOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, [value]).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    public async Task IsInvalid_If_CrashSequence_Has_IncorrectValue()
    {
        var provider = new CrashDumpCommandLineProvider();
        CommandLineOption option = provider.GetCommandLineOptions().First(x => x.Name == CrashDumpCommandLineOptions.CrashSequenceOptionName);

        ValidationResult validateOptionsResult = await provider.ValidateOptionArgumentsAsync(option, ["maybe"]).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.AreEqual(CrashDumpResources.CrashSequenceOptionInvalidArgument, validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report is not supported on Windows (dotnet/runtime#80191)")]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpFileNameOptionName)]
    [DataRow(CrashDumpCommandLineOptions.CrashDumpTypeOptionName)]
    public async Task If_CrashReportMainOption_IsSpecified_ShouldReturn_IsValid(string crashDumpArgument)
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { crashDumpArgument, [] },
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report is not supported on Windows (dotnet/runtime#80191)")]
    public async Task CrashReport_Without_CrashDump_Is_Valid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Crash report is not supported on Windows (dotnet/runtime#80191)")]
    public async Task CrashReport_Alongside_CrashDump_Is_Valid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsTrue(validateOptionsResult.IsValid);
        Assert.IsTrue(string.IsNullOrEmpty(validateOptionsResult.ErrorMessage));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates Windows-specific rejection of --crash-report")]
    public async Task CrashReport_OnWindows_IsInvalid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.Contains("'--crash-report' is not supported on Windows", validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates Windows-specific rejection of --crash-report")]
    public async Task CrashReport_WithCrashDump_OnWindows_IsInvalid()
    {
        var provider = new CrashDumpCommandLineProvider();
        var options = new Dictionary<string, string[]>
        {
            { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            { CrashDumpCommandLineOptions.CrashReportOptionName, [] },
        };

        ValidationResult validateOptionsResult = await provider.ValidateCommandLineOptionsAsync(new TestCommandLineOptions(options)).ConfigureAwait(false);
        Assert.IsFalse(validateOptionsResult.IsValid);
        Assert.Contains("'--crash-report' is not supported on Windows", validateOptionsResult.ErrorMessage);
    }

    [TestMethod]
    [DataRow("MyApp_%p_crash.dmp", @"^MyApp_.*_crash\.dmp$")]
    [DataRow("%e_%p_crash.dmp", @"^.*_.*_crash\.dmp$")]
    [DataRow("%p%t_crash.dmp", @"^.*_crash\.dmp$")]
    [DataRow("customdumpname.dmp", @"^customdumpname\.dmp$")]
    [DataRow("dump_%p_%t_%h.dmp", @"^dump_.*_.*_.*\.dmp$")]
    [DataRow("trailing%", "^trailing%$")]
    // Glob metacharacters that may appear literally in a user-supplied filename must be escaped so they are
    // matched literally, not treated as wildcards. This guards against picking up unrelated dump files on
    // file systems that allow these characters in file names (e.g. Linux/macOS).
    [DataRow("my*dump_%p.dmp", @"^my\*dump_.*\.dmp$")]
    [DataRow("dump?_%p.dmp", @"^dump\?_.*\.dmp$")]
    // The .NET runtime's createdump tool treats "%%" as an escape for a literal '%'. The regex builder
    // must preserve that: "%%" stays a literal percent rather than collapsing into a ".*" wildcard
    // (which would otherwise over-match unrelated files).
    [DataRow("My%%App_%p.dmp", @"^My%App_.*\.dmp$")]
    [DataRow("%%%p.dmp", @"^%.*\.dmp$")]
    [DataRow("%p%%.dmp", @"^.*%\.dmp$")]
    public void BuildDumpFileNameRegexPattern_ConvertsPlaceholdersToRegex(string fileName, string expected)
    {
        string actual = CrashDumpProcessLifetimeHandler.BuildDumpFileNameRegexPattern(fileName);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void BuildDumpFileNameRegex_LiteralGlobMetacharactersInName_DoesNotOverMatch()
    {
        Regex regex = CrashDumpProcessLifetimeHandler.BuildDumpFileNameRegex("my*dump_%p.dmp");
        Assert.IsTrue(regex.IsMatch("my*dump_123.dmp"), "Literal '*' must be matched literally.");
        Assert.IsFalse(regex.IsMatch("myXYZdump_123.dmp"), "Literal '*' must not act as a wildcard.");
        Assert.IsFalse(regex.IsMatch("mydump_123.dmp"), "Literal '*' must require at least the '*' character to be present.");
    }

    [TestMethod]
    public void BuildDumpFileNameRegex_LiteralPercentInName_DoesNotOverMatch()
    {
        Regex regex = CrashDumpProcessLifetimeHandler.BuildDumpFileNameRegex("My%%App_%p.dmp");
        Assert.IsTrue(regex.IsMatch("My%App_123.dmp"), "Literal '%' must be matched literally.");
        Assert.IsFalse(regex.IsMatch("MyApp_123.dmp"), "Literal '%' must require the '%' character to be present.");
        Assert.IsFalse(regex.IsMatch("MyXApp_123.dmp"), "Literal '%' must not be treated as a wildcard.");
    }

    [TestMethod]
    [DataRow("dump_%p.dmp")]
    [DataRow("")]
    [DataRow("customdumpname.dmp")]
    public void GetDumpDirectory_WhenPatternHasNoDirectoryComponent_ReturnsCurrentDirectory(string pattern)
    {
        // The CrashDump runtime writes dumps to the current working directory when the configured pattern
        // contains no directory prefix. Previously the extension's enumeration was silently skipped in that
        // case because Path.GetDirectoryName returns "" (not null), and Directory.Exists("") is false.
        string actual = CrashDumpProcessLifetimeHandler.GetDumpDirectory(pattern);

        Assert.AreEqual(".", actual);
    }

    [TestMethod]
    public void GetDumpDirectory_WhenPatternHasDirectoryComponent_ReturnsDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dumps");
        string pattern = Path.Combine(directory, "dump_%p.dmp");

        string actual = CrashDumpProcessLifetimeHandler.GetDumpDirectory(pattern);

        Assert.AreEqual(directory, actual);
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_OnlyPublishesDumpsThatAppearedDuringTheRun()
    {
        // Create an isolated dump directory so we can pre-populate it with stale files that simulate
        // dumps left over from a previous run (the snapshot must filter them out) and then write a
        // fresh dump that should be published as an artifact.
        string dumpDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "crashdump-tests-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string stale1 = Path.Combine(dumpDirectory, "CrashDump_999_crash.dmp");
            string stale2 = Path.Combine(dumpDirectory, "CrashDump_888_crash.dmp");
            File.WriteAllText(stale1, "stale");
            File.WriteAllText(stale2, "stale");

            string dumpPattern = Path.Combine(dumpDirectory, "CrashDump_%p_crash.dmp");
            var configuration = new CrashDumpConfiguration { DumpFileNamePattern = dumpPattern };
            var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            });
            var messageBus = new RecordingMessageBus();
            var outputDevice = new NullOutputDevice();
            var handler = new CrashDumpProcessLifetimeHandler(commandLineOptions, messageBus, outputDevice, configuration);

            // Snapshot the directory; both stale dumps must be considered pre-existing.
            await handler.OnTestHostProcessStartedAsync(new TestHostProcessInformation(pid: 123, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            // Simulate the runtime writing two new dumps during the run: one for the testhost and one
            // for a child process that crashed too. The expected testhost dump is also created on disk
            // (so the "missing expected dump" warning is not triggered).
            string fresh1 = Path.Combine(dumpDirectory, "CrashDump_123_crash.dmp");
            string fresh2 = Path.Combine(dumpDirectory, "CrashDump_456_crash.dmp");
            File.WriteAllText(fresh1, "fresh");
            File.WriteAllText(fresh2, "fresh");

            await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(pid: 123, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            string[] publishedDumps = messageBus.Published
                .OfType<FileArtifact>()
                .Select(static a => a.FileInfo.FullName)
                .OrderBy(static p => p, StringComparer.Ordinal)
                .ToArray();
            string[] expected = new[] { fresh1, fresh2 }.OrderBy(static p => p, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, publishedDumps);
        }
        finally
        {
            try
            {
                Directory.Delete(dumpDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_PatternWithMultiplePlaceholders_DoesNotEmitFalseMissingDumpWarning()
    {
        // When the configured dump pattern relies on placeholders other than `%p` (here `%e`),
        // the literal-`%p`-substituted path never exists on disk. The handler must therefore
        // recognize the testhost dump from the regex match (PID-baked into the testhost-specific
        // regex) and avoid emitting CannotFindExpectedCrashDumpFile, which would otherwise
        // contradict the success banner above it.
        string dumpDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "crashdump-tests-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string dumpPattern = Path.Combine(dumpDirectory, "Dump_%e_%p.dmp");
            var configuration = new CrashDumpConfiguration { DumpFileNamePattern = dumpPattern };
            var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            });
            var messageBus = new RecordingMessageBus();
            var outputDevice = new CapturingOutputDevice();
            var handler = new CrashDumpProcessLifetimeHandler(commandLineOptions, messageBus, outputDevice, configuration);

            await handler.OnTestHostProcessStartedAsync(new TestHostProcessInformation(pid: 123, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            // Runtime expands %e to "testhost" and %p to the actual PID. The resulting filename is
            // matched by the testhost-specific regex even though the literal-%p substitution would
            // produce `Dump_%e_123.dmp` (which does not exist on disk).
            string testhostDump = Path.Combine(dumpDirectory, "Dump_testhost_123.dmp");
            File.WriteAllText(testhostDump, "fresh");

            await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(pid: 123, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            string[] publishedDumps = messageBus.Published
                .OfType<FileArtifact>()
                .Select(static a => a.FileInfo.FullName)
                .ToArray();
            CollectionAssert.AreEqual(new[] { testhostDump }, publishedDumps);

            // The "expected dump not found" warning must NOT be emitted: the testhost dump was
            // recognized via the regex even though `expectedDumpFile` (literal `%p` substitution)
            // would be `Dump_%e_123.dmp`, which does not exist on disk.
            string captured = string.Join(" | ", outputDevice.Displayed);
            Assert.DoesNotContain("Dump_%e_123.dmp", captured, "CannotFindExpectedCrashDumpFile must not be displayed when the testhost dump was recognized via the regex.");
        }
        finally
        {
            try
            {
                Directory.Delete(dumpDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_PatternWithRepeatedPidPlaceholder_RecognizesTesthostDump()
    {
        // A configured pattern can include `%p` more than once. Verify that the PID-substitution
        // applies to every occurrence (string.Replace substitutes all matches) so the
        // testhost-specific regex is anchored to the actual PID on both sides.
        string dumpDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "crashdump-tests-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string dumpPattern = Path.Combine(dumpDirectory, "dump_%p_backup_%p.dmp");
            var configuration = new CrashDumpConfiguration { DumpFileNamePattern = dumpPattern };
            var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            });
            var messageBus = new RecordingMessageBus();
            var outputDevice = new CapturingOutputDevice();
            var handler = new CrashDumpProcessLifetimeHandler(commandLineOptions, messageBus, outputDevice, configuration);

            await handler.OnTestHostProcessStartedAsync(new TestHostProcessInformation(pid: 555, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            string testhostDump = Path.Combine(dumpDirectory, "dump_555_backup_555.dmp");
            File.WriteAllText(testhostDump, "fresh");

            await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(pid: 555, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            string[] publishedDumps = messageBus.Published
                .OfType<FileArtifact>()
                .Select(static a => a.FileInfo.FullName)
                .ToArray();
            CollectionAssert.AreEqual(new[] { testhostDump }, publishedDumps);
            string captured = string.Join(" | ", outputDevice.Displayed);
            Assert.DoesNotContain("dump_555_backup_555.dmp", captured, "CannotFindExpectedCrashDumpFile must not be displayed when the testhost dump was recognized via the regex.");
        }
        finally
        {
            try
            {
                Directory.Delete(dumpDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    [TestMethod]
    public async Task OnTestHostProcessExitedAsync_TesthostAndChildBothCrashWithMultiPlaceholderPattern_PublishesBothAndSuppressesWarning()
    {
        // Regression coverage for the M1 vector: when both the testhost AND a child process write
        // dumps using a pattern with non-`%p` placeholders, both dumps must be published, and the
        // "expected dump not found" warning must NOT fire (because the testhost dump is identified
        // via the testhost-specific regex, not via File.Exists on the literal-`%p`-substituted path).
        string dumpDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "crashdump-tests-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string dumpPattern = Path.Combine(dumpDirectory, "Dump_%e_%p.dmp");
            var configuration = new CrashDumpConfiguration { DumpFileNamePattern = dumpPattern };
            var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
            {
                { CrashDumpCommandLineOptions.CrashDumpOptionName, [] },
            });
            var messageBus = new RecordingMessageBus();
            var outputDevice = new CapturingOutputDevice();
            var handler = new CrashDumpProcessLifetimeHandler(commandLineOptions, messageBus, outputDevice, configuration);

            await handler.OnTestHostProcessStartedAsync(new TestHostProcessInformation(pid: 123, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            string testhostDump = Path.Combine(dumpDirectory, "Dump_testhost_123.dmp");
            string childDump = Path.Combine(dumpDirectory, "Dump_child_456.dmp");
            File.WriteAllText(testhostDump, "fresh");
            File.WriteAllText(childDump, "fresh");

            await handler.OnTestHostProcessExitedAsync(new TestHostProcessInformation(pid: 123, exitCode: 1, hasExitedGracefully: false), CancellationToken.None);

            string[] publishedDumps = messageBus.Published
                .OfType<FileArtifact>()
                .Select(static a => a.FileInfo.FullName)
                .OrderBy(static p => p, StringComparer.Ordinal)
                .ToArray();
            string[] expected = new[] { testhostDump, childDump }.OrderBy(static p => p, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, publishedDumps);
            string captured = string.Join(" | ", outputDevice.Displayed);
            Assert.DoesNotContain("Dump_%e_123.dmp", captured, "CannotFindExpectedCrashDumpFile must not be displayed when the testhost dump was recognized via the regex.");
        }
        finally
        {
            try
            {
                Directory.Delete(dumpDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }

    private sealed class RecordingMessageBus : IMessageBus
    {
        public List<IData> Published { get; } = [];

        public Task PublishAsync(IDataProducer dataProducer, IData data)
        {
            Published.Add(data);
            return Task.CompletedTask;
        }
    }

    private sealed class NullOutputDevice : IOutputDevice
    {
        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class CapturingOutputDevice : IOutputDevice
    {
        public List<string> Displayed { get; } = [];

        public Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        {
            if (data is ErrorMessageOutputDeviceData errorData)
            {
                Displayed.Add(errorData.Message);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestHostProcessInformation : ITestHostProcessInformation
    {
        public TestHostProcessInformation(int pid, int exitCode, bool hasExitedGracefully)
        {
            PID = pid;
            ExitCode = exitCode;
            HasExitedGracefully = hasExitedGracefully;
        }

        public int PID { get; }

        public int ExitCode { get; }

        public bool HasExitedGracefully { get; }
    }
}
