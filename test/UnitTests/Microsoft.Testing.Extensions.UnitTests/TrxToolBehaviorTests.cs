// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxToolBehaviorTests
{
    private static readonly XNamespace Ns = TrxToolWorkspace.Ns;

    [TestMethod]
    [DataRow("none", false)]
    [DataRow("single", false)]
    [DataRow("missing", false)]
    [DataRow("extension", false)]
    [DataRow("directory", false)]
    [DataRow("valid-upper-case", true)]
    [DataRow("three", true)]
    [DataRow("same-path-twice", true)]
    public async Task MergeCommandLine_ValidatesInputsByArityExtensionAndExistence(string shape, bool valid)
    {
        using var files = new TrxToolWorkspace();
        string first = files.Write("first.trx", "Validation checks existence, not XML content.");
        string second = files.Write("second.TRX", string.Empty);
        string wrongExtension = files.Write("third.xml", string.Empty);
        string directory = files.FilePath("folder.trx");
        Directory.CreateDirectory(directory);
        string[] arguments = shape switch
        {
            "none" => [],
            "single" => [first],
            "missing" => [first, files.FilePath("absent.trx")],
            "extension" => [first, wrongExtension],
            "directory" => [first, directory],
            "three" => [first, second, first],
            "same-path-twice" => [first, first],
            _ => [first, second],
        };
        TrxMergeToolCommandLine provider = new ToolTrxMergeFactory().CreateCommandLine();
        CommandLineOption option = provider.GetCommandLineOptions().Single(value => value.Name == TrxMergeToolCommandLine.InputOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, arguments);

        Assert.AreEqual(valid, result.IsValid);
        Assert.AreEqual(valid ? null : TrxReport.Resources.ExtensionResources.TrxMergeToolInvalidInputs, result.ErrorMessage);
        Assert.AreEqual("Validation checks existence, not XML content.", File.ReadAllText(first));
        Assert.IsFalse(File.Exists(files.FilePath("absent.trx")));
    }

    [TestMethod]
    [DataRow("none", false)]
    [DataRow("two", false)]
    [DataRow("extension", false)]
    [DataRow("new-output", true)]
    [DataRow("new-upper-case-output", true)]
    public async Task MergeCommandLine_ValidatesOutputWithoutRequiringExistingFile(string shape, bool valid)
    {
        using var files = new TrxToolWorkspace();
        string lower = files.FilePath(Path.Combine("not-created", "merged.trx"));
        string upper = files.FilePath(Path.Combine("not-created", "merged.TRX"));
        string[] arguments = shape switch
        {
            "none" => [],
            "two" => [lower, upper],
            "extension" => [files.FilePath("merged.xml")],
            "new-upper-case-output" => [upper],
            _ => [lower],
        };
        TrxMergeToolCommandLine provider = new ToolTrxMergeFactory().CreateCommandLine();
        CommandLineOption option = provider.GetCommandLineOptions().Single(value => value.Name == TrxMergeToolCommandLine.OutputOptionName);

        ValidationResult result = await provider.ValidateOptionArgumentsAsync(option, arguments);

        Assert.AreEqual(valid, result.IsValid);
        Assert.AreEqual(valid ? null : TrxReport.Resources.ExtensionResources.TrxMergeToolInvalidOutput, result.ErrorMessage);
        Assert.IsEmpty(Directory.GetFileSystemEntries(files.Root), "Validation must not materialize the output or its directory.");
    }

    [TestMethod]
    public async Task CommandLines_UnknownOptionsDoNotPerformFileValidation()
    {
        var mergeFactory = new ToolTrxMergeFactory();
        var compareFactory = new ToolTrxCompareFactory();
        var unknown = new CommandLineOption("unrelated", "not a TRX option", ArgumentArity.ExactlyOne, false);
        string[] arguments = ["never-open-this.invalid"];

        ValidationResult merge = await mergeFactory.CreateCommandLine().ValidateOptionArgumentsAsync(unknown, arguments);
        ValidationResult compare = await compareFactory.CreateTrxCompareToolCommandLine().ValidateOptionArgumentsAsync(unknown, arguments);
        ValidationResult commandLine = await mergeFactory.CreateCommandLine().ValidateCommandLineOptionsAsync(new TestCommandLineOptions([]));

        Assert.IsTrue(merge.IsValid);
        Assert.IsNull(merge.ErrorMessage);
        Assert.IsTrue(compare.IsValid);
        Assert.IsNull(compare.ErrorMessage);
        Assert.IsTrue(commandLine.IsValid);
        Assert.IsNull(commandLine.ErrorMessage);
    }

    [TestMethod]
    [DataRow("missing-input")]
    [DataRow("missing-output")]
    [DataRow("multiple-output")]
    public async Task MergeTool_MissingOrMultipleRuntimeOptionsRejectBeforeIo(string shape)
    {
        using var files = new TrxToolWorkspace();
        var options = new Dictionary<string, string[]>();
        if (shape != "missing-input")
        {
            options[TrxMergeToolCommandLine.InputOptionName] = [files.FilePath("never-read-one.trx"), files.FilePath("never-read-two.trx")];
        }

        if (shape != "missing-output")
        {
            options[TrxMergeToolCommandLine.OutputOptionName] = shape == "multiple-output"
                ? [files.FilePath("one.trx"), files.FilePath("two.trx")] : [files.FilePath("merged.trx")];
        }

        TrxMergeTool tool = new ToolTrxMergeFactory().CreateTool(new TestCommandLineOptions(options));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => tool.RunAsync(CancellationToken.None));

        Assert.Contains("Unexpected state", exception.Message);
        Assert.IsEmpty(Directory.GetFileSystemEntries(files.Root));
    }

    [TestMethod]
    public async Task MergeTool_ProducesLogicalReportWithoutMutatingInputs()
    {
        using var files = new TrxToolWorkspace();
        XDocument firstReport = TrxToolWorkspace.Report("test-one", "Alpha");
        XDocument secondReport = TrxToolWorkspace.Report("test-two", "Beta", failed: true);
        TrxToolWorkspace.Summary(firstReport).Add(TrxToolWorkspace.Collector(new XElement(Ns + "A", new XAttribute("href", "trace.log"))));
        TrxToolWorkspace.Summary(secondReport).Add(TrxToolWorkspace.Collector(new XElement(Ns + "A", new XAttribute("href", "trace.log"))));
        string first = files.Save(Path.Combine("one", "report.trx"), firstReport);
        string second = files.Save(Path.Combine("two", "report.trx"), secondReport);
        string firstAttachment = files.Write(Path.Combine("one", "deploy", "In", "trace.log"), "first module evidence");
        string secondAttachment = files.Write(Path.Combine("two", "deploy", "In", "trace.log"), "second module evidence");
        byte[] firstBytes = File.ReadAllBytes(first);
        byte[] secondBytes = File.ReadAllBytes(second);
        string output = files.Write(Path.Combine("out", "merged.trx"), new string('x', 30_000));
        var factory = new ToolTrxMergeFactory();
        TrxMergeTool tool = factory.CreateTool(Options([first, second], output));

        Assert.IsTrue(await factory.IsEnabledAsync());
        Assert.IsTrue(await tool.IsEnabledAsync());
        Assert.AreEqual(0, await tool.RunAsync(CancellationToken.None));

        var merged = XDocument.Load(output);
        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxMergedRunName, (string?)merged.Root!.Attribute("name"));
        var runId = Guid.Parse((string)merged.Root.Attribute("id")!);
        Assert.AreNotEqual(Guid.Empty, runId);
        Assert.AreSequenceEqual(["Alpha", "Beta"], merged.Descendants(Ns + "UnitTestResult").Select(value => (string?)value.Attribute("testName")));
        Assert.AreEqual("Failed", (string?)TrxToolWorkspace.Summary(merged).Attribute("outcome"));
        XElement counters = merged.Descendants(Ns + "Counters").Single();
        Assert.AreSequenceEqual(["2", "2", "1", "1"], new[] { "total", "executed", "passed", "failed" }.Select(name => (string?)counters.Attribute(name)));
        Assert.HasCount(2, merged.Descendants(Ns + "UnitTest"));
        Assert.HasCount(2, merged.Descendants(Ns + "TestEntry"));
        Assert.HasCount(1, merged.Descendants(Ns + "TestList"));
        Assert.AreSequenceEqual(["0/trace.log", "1/trace.log"], merged.Descendants(Ns + "A").Select(value => (string?)value.Attribute("href")));
        string deployment = TrxToolWorkspace.Deployment(merged);
        Assert.AreEqual("first module evidence", File.ReadAllText(Path.Combine(Path.GetDirectoryName(output)!, deployment, "In", "0", "trace.log")));
        Assert.AreEqual("second module evidence", File.ReadAllText(Path.Combine(Path.GetDirectoryName(output)!, deployment, "In", "1", "trace.log")));
        Assert.AreSequenceEqual(firstBytes, File.ReadAllBytes(first));
        Assert.AreSequenceEqual(secondBytes, File.ReadAllBytes(second));
        Assert.AreEqual("first module evidence", File.ReadAllText(firstAttachment));
        Assert.AreEqual("second module evidence", File.ReadAllText(secondAttachment));
        Assert.IsLessThan(30_000L, new FileInfo(output).Length);

        string reordered = files.FilePath(Path.Combine("out", "reordered.trx"));
        Assert.AreEqual(0, await factory.CreateTool(Options([second, first], reordered)).RunAsync(CancellationToken.None));
        Assert.AreEqual(runId.ToString(), (string?)XDocument.Load(reordered).Root!.Attribute("id"), "The logical identity is independent of CLI input ordering.");
        string copiedInput = files.Save(Path.Combine("three", "report.trx"), firstReport);
        string distinct = files.FilePath(Path.Combine("out", "distinct.trx"));
        Assert.AreEqual(0, await factory.CreateTool(Options([copiedInput, second], distinct)).RunAsync(CancellationToken.None));
        Assert.AreNotEqual(runId.ToString(), (string?)XDocument.Load(distinct).Root!.Attribute("id"), "A different source identity must not reuse a constant run ID.");
        Assert.IsEmpty(Directory.GetFiles(files.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow("canceled")]
    [DataRow("malformed")]
    [DataRow("missing")]
    public async Task MergeTool_CancellationOrMalformedInputPreservesExistingOutput(string failure)
    {
        using var files = new TrxToolWorkspace();
        string first = files.Save("first.trx", TrxToolWorkspace.Report("first", "First"));
        string second = failure == "malformed"
            ? files.Write("second.trx", "<TestRun")
            : failure == "missing" ? files.FilePath("missing.trx") : files.Save("second.trx", TrxToolWorkspace.Report("second", "Second"));
        byte[] original = File.ReadAllBytes(first);
        string output = files.Write("merged.trx", "previous complete report");
        TrxMergeTool tool = new ToolTrxMergeFactory().CreateTool(Options([first, second], output));
        using var cancellation = new CancellationTokenSource();

        if (failure == "canceled")
        {
            cancellation.Cancel();
            OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => tool.RunAsync(cancellation.Token));
            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        }
        else if (failure == "malformed")
        {
            await Assert.ThrowsExactlyAsync<XmlException>(() => tool.RunAsync(CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsExactlyAsync<FileNotFoundException>(() => tool.RunAsync(CancellationToken.None));
        }

        Assert.AreEqual("previous complete report", File.ReadAllText(output));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(first));
        Assert.IsEmpty(Directory.GetFiles(files.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TrxMerge_OutputIsInput")]
    [DataRow(true, DisplayName = "TrxMerge_OutputLexicallyAliasesInput")]
    public async Task MergeTool_OutputAliasCannotOverwriteInput(bool normalize)
    {
        using var files = new TrxToolWorkspace();
        string first = files.Save("first.trx", TrxToolWorkspace.Report("first", "First"));
        string second = files.Save("second.trx", TrxToolWorkspace.Report("second", "Second"));
        byte[] original = File.ReadAllBytes(first);
        string output = normalize ? Path.Combine(files.Root, "unused", "..", "first.trx") : first;

        ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => new ToolTrxMergeFactory().CreateTool(Options([first, second], output)).RunAsync(CancellationToken.None));

        Assert.AreEqual("outputPath", exception.ParamName);
        Assert.Contains("inputs are treated as read-only", exception.Message);
        Assert.AreSequenceEqual(original, File.ReadAllBytes(first));
        Assert.IsEmpty(Directory.GetFiles(files.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task MergeTool_ReplacementFailureCleansTemporarySibling()
    {
        using var files = new TrxToolWorkspace();
        string first = files.Save("first.trx", TrxToolWorkspace.Report("first", "First"));
        string second = files.Save("second.trx", TrxToolWorkspace.Report("second", "Second"));
        byte[] original = File.ReadAllBytes(first);
        string output = files.FilePath("directory.trx");
        Directory.CreateDirectory(output);

        Exception exception = await Assert.ThrowsAsync<Exception>(
            () => new ToolTrxMergeFactory().CreateTool(Options([first, second], output)).RunAsync(CancellationToken.None));

        Assert.IsTrue(
            exception is IOException or UnauthorizedAccessException,
            "The platform-specific directory replacement failure must propagate, not be reported as a successful merge.");
        Assert.IsTrue(Directory.Exists(output));
        Assert.IsEmpty(Directory.GetFiles(files.Root, "*.tmp", SearchOption.AllDirectories));
        Assert.AreSequenceEqual(original, File.ReadAllBytes(first));
    }

    [TestMethod]
    public async Task CompareFactory_CreatedToolAndProviderRemainUsable()
    {
        using var files = new TrxToolWorkspace();
        using var fixture = new TrxBehaviorFixture();
        string baseline = files.Save("baseline.trx", TrxToolWorkspace.Report("baseline-id", "Same test"));
        string compared = files.Save("compared.trx", TrxToolWorkspace.Report("other-id", "Same test", failed: true));
        var options = new TestCommandLineOptions(new()
        {
            [TrxCompareToolCommandLine.BaselineTrxOptionName] = [baseline],
            [TrxCompareToolCommandLine.TrxToCompareOptionName] = [compared],
        });
        var factory = new ToolTrxCompareFactory();
        TrxCompareToolCommandLine provider = factory.CreateTrxCompareToolCommandLine();
        TrxCompareTool tool = factory.CreateTrxCompareTool(options, fixture.Output.Object, fixture.Scheduler);
        Assert.IsTrue(await factory.IsEnabledAsync());
        Assert.IsTrue(await provider.IsEnabledAsync());
        Assert.IsTrue(await tool.IsEnabledAsync());
        Assert.AreEqual(factory.Uid, provider.Uid);
        Assert.AreEqual(factory.Uid, tool.Uid);
        Assert.AreEqual(provider.ToolName, tool.Name);
        Assert.IsTrue((await provider.ValidateCommandLineOptionsAsync(options)).IsValid);
        foreach (CommandLineOption option in provider.GetCommandLineOptions())
        {
            Assert.IsTrue(options.TryGetOptionArgumentList(option.Name, out string[]? values));
            Assert.IsTrue((await provider.ValidateOptionArgumentsAsync(option, values)).IsValid);
        }

        int exitCode = await tool.RunAsync(CancellationToken.None);

        Assert.AreEqual(1, exitCode);
        (IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken token) = Assert.ContainsSingle(fixture.Displays);
        Assert.AreSame(tool, producer);
        string output = Assert.IsInstanceOfType<TextOutputDeviceData>(data).Text;
        Assert.Contains("Test 'Suite.Same test' has a different outcome. Got 'Failed', expected 'Passed'", output);
        Assert.Contains("Test 'Suite.Same test' has a different outcome. Got 'Passed', expected 'Failed'", output);
        Assert.EndsWith("Comparison check failed!" + Environment.NewLine, output);
    }

#if NETCOREAPP
    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task MergeTool_LinuxSymlinkedOutputAliasPreservesInput()
    {
        using var files = new TrxToolWorkspace();
        string first = files.Save(Path.Combine("source", "first.trx"), TrxToolWorkspace.Report("first", "First"));
        string second = files.Save(Path.Combine("source", "second.trx"), TrxToolWorkspace.Report("second", "Second"));
        byte[] original = File.ReadAllBytes(first);
        string link = files.FilePath("linked");
        Directory.CreateSymbolicLink(link, files.FilePath("source"));

        ArgumentException exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => new ToolTrxMergeFactory().CreateTool(Options([first, second], Path.Combine(link, "first.trx"))).RunAsync(CancellationToken.None));

        Assert.AreEqual("outputPath", exception.ParamName);
        Assert.AreSequenceEqual(original, File.ReadAllBytes(first));
        Assert.IsNotNull(new DirectoryInfo(link).LinkTarget);
        Assert.IsEmpty(Directory.GetFiles(files.FilePath("source"), "*.tmp"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public async Task MergeTool_LinuxCyclicOutputParentDoesNotOverwriteInput()
    {
        using var files = new TrxToolWorkspace();
        string first = files.Save("first.trx", TrxToolWorkspace.Report("first", "First"));
        string second = files.Save("second.trx", TrxToolWorkspace.Report("second", "Second"));
        byte[] original = File.ReadAllBytes(first);
        string link = files.FilePath("loop");
        Directory.CreateSymbolicLink(link, "loop");

        await Assert.ThrowsAsync<IOException>(
            () => new ToolTrxMergeFactory().CreateTool(Options([first, second], Path.Combine(link, "merged.trx"))).RunAsync(CancellationToken.None));

        Assert.AreSequenceEqual(original, File.ReadAllBytes(first));
        Assert.IsNotNull(new DirectoryInfo(link).LinkTarget);
        Assert.IsEmpty(Directory.GetFiles(files.Root, "*.tmp", SearchOption.TopDirectoryOnly));
    }
#endif

    private static TestCommandLineOptions Options(string[] inputs, string output)
        => new(new()
        {
            [TrxMergeToolCommandLine.InputOptionName] = inputs,
            [TrxMergeToolCommandLine.OutputOptionName] = [output],
        });
}
