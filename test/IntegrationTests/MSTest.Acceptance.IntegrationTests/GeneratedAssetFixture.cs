// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

public abstract class GeneratedAssetFixture : ITestAssetFixture
{
    private readonly TempDirectory _tempDirectory = new();
    private TestAsset? _asset;

    protected abstract string ProjectName { get; }

    protected abstract string SourceFiles { get; }

    protected virtual string AdditionalProjectItems => string.Empty;

    public string TargetAssetPath => _asset!.TargetAssetPath;

    public string AssemblyPath => GetTestHost().FullName;

    public TestHost GetTestHost()
        => TestHost.LocateFrom(TargetAssetPath, ProjectName, "net462");

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string code = ProjectFile
            .PatchCodeWithReplace("$ProjectName$", ProjectName)
            .PatchCodeWithReplace("$MSTestVersion$", AcceptanceTestBase.MSTestVersion)
            .PatchCodeWithReplace("$TestPlatformVersion$", GeneratedAssetSource.GetPackageVersion("Microsoft.TestPlatform.ObjectModel"))
            .PatchCodeWithReplace("$AdditionalProjectItems$", AdditionalProjectItems)
            + AdapterHarness
            + GeneratedAssetSource.FromFiles(
                @"test\IntegrationTests\MSTest.Acceptance.IntegrationTests\TestCaseFilterFactory.cs")
            + SourceFiles;

        _asset = await TestAsset.GenerateAssetAsync(ProjectName, code, _tempDirectory);
        await DotnetCli.RunAsync(
            $"build \"{TargetAssetPath}\" -c Release",
            callerMemberName: ProjectName,
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        _asset?.Dispose();
        _tempDirectory.Dispose();
    }

    private const string ProjectFile = """
        #file $ProjectName$.csproj
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <TargetFramework>net462</TargetFramework>
            <OutputType>Exe</OutputType>
            <EnableMSTestRunner>false</EnableMSTestRunner>
            <ImplicitUsings>enable</ImplicitUsings>
            <LangVersion>preview</LangVersion>
            <Nullable>enable</Nullable>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
            <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
            <PackageReference Include="Microsoft.TestPlatform.ObjectModel" Version="$TestPlatformVersion$" />
          </ItemGroup>

          <ItemGroup>
            <Using Include="System.Collections" />
            <Using Include="System.Collections.Concurrent" />
            <Using Include="System.Diagnostics" />
            <Using Include="System.Diagnostics.CodeAnalysis" />
            <Using Include="System.Globalization" />
            <Using Include="System.Reflection" />
            <Using Include="System.Runtime.CompilerServices" />
            <Using Include="System.Runtime.InteropServices" />
            <Using Include="System.Runtime.Versioning" />
            <Using Include="System.Text" />
            <Using Include="System.Text.RegularExpressions" />
            <Using Include="System.Xml" />
            <Using Include="System.Xml.Linq" />
            <Using Include="System.Xml.XPath" />
          </ItemGroup>

          $AdditionalProjectItems$

        </Project>

        """;

    private const string AdapterHarness = """
        #file Program.cs
        using System.Collections.Immutable;
        using System.Text;
        using DiscoveryAndExecutionTests.Utilities;
        using Microsoft.VisualStudio.TestPlatform.ObjectModel;
        using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
        using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
        using VsTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

        internal static class Program
        {
            private const string TestCasePrefix = "##MSTEST-TESTCASE##";
            private const string TestResultPrefix = "##MSTEST-TESTRESULT##";
            private const string DiscovererTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.MSTestDiscoverer";
            private const string ExecutorTypeName = "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.MSTestExecutor";

            public static int Main(string[] args)
            {
                try
                {
                    int commandIndex = Array.FindIndex(
                        args,
                        argument => argument is "--adapter-discover" or "--adapter-run");
                    if (commandIndex < 0)
                    {
                        throw new ArgumentException("No adapter command was provided.");
                    }

                    return args[commandIndex] switch
                    {
                        "--adapter-discover" => Discover(DecodeArgument(args[commandIndex + 1])),
                        "--adapter-run" => Run(
                            DecodeArgument(args[commandIndex + 1])!,
                            DecodeArgument(args[commandIndex + 2]),
                            args[commandIndex + 3] == "1"),
                        _ => throw new InvalidOperationException(),
                    };
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                    return 1;
                }
            }

            private static int Discover(string? filter)
            {
                foreach (TestCase testCase in DiscoverTests(filter))
                {
                    Console.WriteLine(TestCasePrefix + SerializeTestCase(testCase));
                }

                return 0;
            }

            private static int Run(string testIds, string? filter, bool simulateOutOfProcessTestCase)
            {
                var selectedIds = new HashSet<Guid>(
                    testIds
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(Guid.Parse));
                ImmutableArray<TestCase> tests = DiscoverTests(filter: null)
                    .Where(testCase => selectedIds.Contains(testCase.Id))
                    .ToImmutableArray();
                if (simulateOutOfProcessTestCase)
                {
                    tests = tests.Select(SimulateOutOfProcessTestCase).ToImmutableArray();
                }

                var frameworkHandle = new InternalFrameworkHandle();
                CreateAdapter<ITestExecutor>(ExecutorTypeName).RunTests(
                    tests,
                    new InternalRunContext(filter),
                    frameworkHandle);

                if (frameworkHandle.ErrorMessages.Count > 0)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, frameworkHandle.ErrorMessages));
                }

                foreach (VsTestResult testResult in frameworkHandle.Results)
                {
                    Console.WriteLine(TestResultPrefix + SerializeTestResult(testResult));
                }

                return 0;
            }

            private static TestCase SimulateOutOfProcessTestCase(TestCase testCase)
            {
                var outOfProcessTestCase = new TestCase(testCase.FullyQualifiedName, testCase.ExecutorUri, testCase.Source)
                {
                    CodeFilePath = testCase.CodeFilePath,
                    DisplayName = testCase.DisplayName,
                    Id = testCase.Id,
                    LineNumber = testCase.LineNumber,
                };

                foreach (KeyValuePair<TestProperty, object?> property in testCase.GetProperties())
                {
                    outOfProcessTestCase.SetPropertyValue(property.Key, property.Value);
                }

                return outOfProcessTestCase;
            }

            private static ImmutableArray<TestCase> DiscoverTests(string? filter)
            {
                var discoverySink = new InternalTestCaseDiscoverySink();
                var logger = new InternalMessageLogger();
                CreateAdapter<ITestDiscoverer>(DiscovererTypeName).DiscoverTests(
                    new[] { Assembly.GetEntryAssembly()!.Location },
                    new InternalDiscoveryContext(filter),
                    logger,
                    discoverySink);

                return logger.ErrorMessages.Count > 0
                    ? throw new InvalidOperationException(string.Join(Environment.NewLine, logger.ErrorMessages))
                    : discoverySink.TestCases.ToImmutableArray();
            }

            private static TAdapter CreateAdapter<TAdapter>(string typeName)
            {
                Assembly adapterAssembly = Assembly.Load("MSTest.TestAdapter");
                Type adapterType = adapterAssembly.GetType(typeName, throwOnError: true)!;
                return (TAdapter)Activator.CreateInstance(adapterType)!;
            }

            private static string SerializeTestCase(TestCase testCase)
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, Encoding.UTF8);
                WriteTestCase(writer, testCase);
                return Convert.ToBase64String(stream.ToArray());
            }

            private static string SerializeTestResult(VsTestResult testResult)
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, Encoding.UTF8);
                WriteTestCase(writer, testResult.TestCase);
                writer.Write((int)testResult.Outcome);
                WriteNullableString(writer, testResult.DisplayName);
                WriteNullableString(writer, testResult.ErrorMessage);
                WriteNullableString(writer, testResult.ErrorStackTrace);
                writer.Write(testResult.Duration.Ticks);
                WriteDateTimeOffset(writer, testResult.StartTime);
                WriteDateTimeOffset(writer, testResult.EndTime);
                writer.Write(testResult.Messages.Count);
                foreach (TestResultMessage message in testResult.Messages)
                {
                    writer.Write(message.Category);
                    writer.Write(message.Text);
                }

                return Convert.ToBase64String(stream.ToArray());
            }

            private static void WriteTestCase(BinaryWriter writer, TestCase testCase)
            {
                writer.Write(testCase.FullyQualifiedName);
                writer.Write(testCase.ExecutorUri.ToString());
                writer.Write(testCase.Source);
                writer.Write(testCase.DisplayName);
                writer.Write(testCase.Id.ToByteArray());
                WriteNullableString(writer, testCase.CodeFilePath);
                writer.Write(testCase.LineNumber);

                string?[]? hierarchy = testCase.GetProperties()
                    .SingleOrDefault(property => property.Key.Id == "TestCase.Hierarchy")
                    .Value as string?[];
                writer.Write(hierarchy?.Length ?? -1);
                if (hierarchy is not null)
                {
                    foreach (string? level in hierarchy)
                    {
                        WriteNullableString(writer, level);
                    }
                }

                Trait[] traits = testCase.Traits.ToArray();
                writer.Write(traits.Length);
                foreach (Trait trait in traits)
                {
                    writer.Write(trait.Name);
                    writer.Write(trait.Value);
                }
            }

            private static void WriteNullableString(BinaryWriter writer, string? value)
            {
                writer.Write(value is not null);
                if (value is not null)
                {
                    writer.Write(value);
                }
            }

            private static void WriteDateTimeOffset(BinaryWriter writer, DateTimeOffset value)
            {
                writer.Write(value.Ticks);
                writer.Write(value.Offset.Ticks);
            }

            private static string? DecodeArgument(string value)
                => value == "~" ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value));

            private sealed class InternalTestCaseDiscoverySink : ITestCaseDiscoverySink
            {
                private readonly List<TestCase> _testCases = new();

                internal IReadOnlyList<TestCase> TestCases => _testCases;

                public void SendTestCase(TestCase discoveredTest)
                    => _testCases.Add(discoveredTest);
            }

            private sealed class InternalDiscoveryContext : IDiscoveryContext
            {
                private readonly string? _filter;

                internal InternalDiscoveryContext(string? filter) => _filter = filter;

                public IRunSettings? RunSettings => null;

                public ITestCaseFilterExpression? GetTestCaseFilter(
                    IEnumerable<string>? supportedProperties,
                    Func<string, TestProperty?>? propertyProvider)
                    => _filter is null ? null : TestCaseFilterFactory.ParseTestFilter(_filter);
            }

            private sealed class InternalRunContext : IRunContext
            {
                private readonly string? _filter;

                internal InternalRunContext(string? filter) => _filter = filter;

                public bool KeepAlive => false;

                public bool InIsolation => false;

                public bool IsBeingDebugged => false;

                public bool IsDataCollectionEnabled => false;

                public IRunSettings? RunSettings => null;

                public string? TestRunDirectory => null;

                public string? SolutionDirectory => null;

                public ITestCaseFilterExpression? GetTestCaseFilter(
                    IEnumerable<string>? supportedProperties,
                    Func<string, TestProperty?>? propertyProvider)
                    => _filter is null ? null : TestCaseFilterFactory.ParseTestFilter(_filter);
            }

            private sealed class InternalFrameworkHandle : IFrameworkHandle
            {
                private readonly List<VsTestResult> _results = new();
                private readonly List<string> _errorMessages = new();

                internal IReadOnlyList<VsTestResult> Results => _results;

                internal IReadOnlyList<string> ErrorMessages => _errorMessages;

                public bool EnableShutdownAfterTestRun { get; set; }

                public int LaunchProcessWithDebuggerAttached(
                    string filePath,
                    string? workingDirectory,
                    string? arguments,
                    IDictionary<string, string?>? environmentVariables)
                    => throw new NotSupportedException();

                public void RecordAttachments(IList<AttachmentSet> attachmentSets)
                {
                }

                public void RecordEnd(TestCase testCase, TestOutcome outcome)
                {
                }

                public void RecordResult(VsTestResult testResult)
                    => _results.Add(testResult);

                public void RecordStart(TestCase testCase)
                {
                }

                public void SendMessage(TestMessageLevel testMessageLevel, string message)
                {
                    if (testMessageLevel == TestMessageLevel.Error)
                    {
                        _errorMessages.Add(message);
                    }
                }
            }

            private sealed class InternalMessageLogger : IMessageLogger
            {
                private readonly List<string> _errorMessages = new();

                internal IReadOnlyList<string> ErrorMessages => _errorMessages;

                public void SendMessage(TestMessageLevel testMessageLevel, string message)
                {
                    if (testMessageLevel == TestMessageLevel.Error)
                    {
                        _errorMessages.Add(message);
                    }
                }
            }
        }

        """;
}

internal static class GeneratedAssetSource
{
    public static string GetPackageVersion(string packageName)
    {
        var centralPackages = XDocument.Load(Path.Combine(RootFinder.Find(), "Directory.Packages.props"));
        string version = centralPackages
            .Descendants("PackageVersion")
            .Single(element => string.Equals(element.Attribute("Include")?.Value, packageName, StringComparison.OrdinalIgnoreCase))
            .Attribute("Version")!
            .Value;

        if (version.StartsWith("$(", StringComparison.Ordinal) && version.EndsWith(')'))
        {
            string propertyName = version[2..^1];
            version = centralPackages.Descendants(propertyName).Single().Value;
        }

        return version;
    }

    public static string FromSharedDirectories(params string[] relativeDirectories)
    {
        var source = new StringBuilder();
        string repoRoot = RootFinder.Find();

        foreach (string relativeDirectory in relativeDirectories)
        {
            string directory = Path.Combine(repoRoot, relativeDirectory);
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                string relativePath = Path.GetRelativePath(directory, file);
                source.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"#file {relativePath}");
                source.AppendLine(File.ReadAllText(file));
            }
        }

        return source.ToString();
    }

    public static string FromFiles(params string[] relativePaths)
    {
        var source = new StringBuilder();
        string repoRoot = RootFinder.Find();
        foreach (string relativePath in relativePaths)
        {
            source.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"#file {Path.GetFileName(relativePath)}");
            source.AppendLine(File.ReadAllText(Path.Combine(repoRoot, relativePath)));
        }

        return source.ToString();
    }

    public const string Cls = """
        #file MyTests.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        [assembly: CLSCompliant(true)]

        namespace DataRowTestProject;

        [TestClass]
        public class ClsTests
        {
            [TestMethod]
            public void TestMethod() => Assert.IsTrue(true);

            [TestMethod]
            [DataRow(10)]
            public void IntDataRow(int i) => Assert.IsTrue(i != 0);

            [TestMethod]
            [DataRow("some string")]
            public void StringDataRow(string s) => Assert.IsNotNull(s);

            [TestMethod]
            [DataRow("some string")]
            [DataRow("some other string")]
            public void StringDataRow2(string s) => Assert.IsNotNull(s);
        }
        """;

    public const string DiscoverInternals = """
        #file UnitTest1.cs
        using System.Runtime.Serialization;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        [assembly: DiscoverInternals]

        namespace DiscoverInternalsProject;

        [TestClass]
        internal class TopLevelInternalClass
        {
            [TestMethod]
            public void TopLevelInternalClass_TestMethod1()
            {
            }

            [TestClass]
            internal class NestedInternalClass
            {
                [TestMethod]
                public void NestedInternalClass_TestMethod1()
                {
                }
            }
        }

        internal class FancyString;

        public abstract class CaseInsensitivityTests<T>
        {
            protected abstract Tuple<T, T> EquivalentInstancesDistinctInCase { get; }

            [TestMethod]
            public void EqualityIsCaseInsensitive()
            {
                Tuple<T, T> tuple = EquivalentInstancesDistinctInCase;
                Assert.AreEqual(tuple.Item1, tuple.Item2);
            }
        }

        [TestClass]
        internal class FancyStringsAreCaseInsensitive : CaseInsensitivityTests<FancyString>
        {
            protected override Tuple<FancyString, FancyString> EquivalentInstancesDistinctInCase
                => new(new FancyString(), new FancyString());
        }

        [DataContract]
        internal sealed class SerializableInternalType;

        [TestClass]
        internal class DynamicDataTest
        {
            [DataTestMethod]
            [DynamicData(nameof(DynamicData))]
            internal void DynamicDataTestMethod(SerializableInternalType serializableInternalType)
            {
            }

            public static IEnumerable<object[]> DynamicData =>
            [
                [new SerializableInternalType()],
            ];
        }
        """;

    public const string Hierarchy = """
        #file ClassWithNoNamespace.cs
        using System.Diagnostics.CodeAnalysis;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        [TestClass]
        [SuppressMessage("Design", "CA1050:Declare types in namespaces", Justification = "We want to test a class with no namespace")]
        public class ClassWithNoNamespace
        {
            [TestMethod]
            public void MyMethodUnderTest()
            {
            }
        }

        #file ClassWithNamespace.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace SomeNamespace.WithMultipleLevels;

        [TestClass]
        public class ClassWithNamespace
        {
            [TestMethod]
            public void MyMethodUnderTest()
            {
            }
        }
        """;

    public const string TestCategories = """
        #file TestCategoriesFromTestDataRowTests.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace TestCategoriesFromTestDataRowProject;

        [TestClass]
        public class TestCategoriesFromTestDataRowTests
        {
            [TestMethod]
            [DynamicData(nameof(GetTestDataWithCategories))]
            public void TestMethodWithDynamicDataCategories(string value, int number)
            {
                Assert.IsTrue(!string.IsNullOrEmpty(value));
                Assert.IsTrue(number > 0);
            }

            public static IEnumerable<TestDataRow<(string Value, int Number)>> GetTestDataWithCategories()
            {
                yield return new TestDataRow<(string, int)>(("value1", 1))
                {
                    TestCategories = ["Integration", "Slow"],
                    DisplayName = "Test with Integration and Slow categories",
                };
                yield return new TestDataRow<(string, int)>(("value2", 2))
                {
                    TestCategories = ["Unit", "Fast"],
                    DisplayName = "Test with Unit and Fast categories",
                };
                yield return new TestDataRow<(string, int)>(("value3", 3))
                {
                    DisplayName = "Test with no additional categories",
                };
            }

            public static IEnumerable<object[]> GetRegularTestData()
            {
                yield return new object[] { "value4", 4 };
            }

            [TestMethod]
            [DynamicData(nameof(GetRegularTestData))]
            public void TestMethodWithRegularData(string value, int number)
            {
                Assert.IsTrue(!string.IsNullOrEmpty(value));
                Assert.IsTrue(number > 0);
            }

            [TestCategory("MethodLevel")]
            [TestMethod]
            [DynamicData(nameof(GetTestDataWithCategoriesForMethodWithCategory))]
            public void TestMethodWithMethodLevelCategoriesAndDataCategories(string value)
                => Assert.IsTrue(!string.IsNullOrEmpty(value));

            public static IEnumerable<TestDataRow<string>> GetTestDataWithCategoriesForMethodWithCategory()
            {
                yield return new TestDataRow<string>("test")
                {
                    TestCategories = ["DataLevel"],
                    DisplayName = "Test with method and data categories",
                };
            }
        }
        """;

    public const string Output = """
        #file Assembly.cs
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        [assembly: Parallelize(Scope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope.MethodLevel, Workers = 0)]

        #file UnitTest1.cs
        using System.Diagnostics;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace OutputTestProject;

        [TestClass]
        public class UnitTest1
        {
            private static readonly Random Rng = new();

            [ClassInitialize]
            public static void ClassInitialize(TestContext testContext) => WriteLines("UnitTest1 - ClassInitialize");

            [TestInitialize]
            public void TestInitialize() => WriteLines("UnitTest1 - TestInitialize");

            [TestCleanup]
            public void TestCleanup() => WriteLines("UnitTest1 - TestCleanup");

            [ClassCleanup]
            public static void ClassCleanup() => WriteLines("UnitTest1 - ClassCleanup");

            [TestMethod]
            public void TestMethod1() => WriteTestOutput("UnitTest1 - TestMethod1");

            [TestMethod]
            public void TestMethod2() => WriteTestOutput("UnitTest1 - TestMethod2");

            [TestMethod]
            public void TestMethod3() => WriteTestOutput("UnitTest1 - TestMethod3");

            private static void WriteTestOutput(string methodName)
            {
                WriteLines($"{methodName} - Call 1");
                Thread.Sleep(Rng.Next(20, 50));
                WriteLines($"{methodName} - Call 2");
                Thread.Sleep(Rng.Next(20, 50));
                WriteLines($"{methodName} - Call 3");
            }

            private static void WriteLines(string message)
            {
                Trace.WriteLine(message);
                Console.WriteLine(message);
                Console.Error.WriteLine(message);
            }
        }

        #file UnitTest2.cs
        using System.Diagnostics;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace OutputTestProject;

        [TestClass]
        public class UnitTest2
        {
            private static readonly Random Rng = new();

            [ClassInitialize]
            public static void ClassInitialize(TestContext testContext) => WriteLines("UnitTest2 - ClassInitialize");

            [TestInitialize]
            public void TestInitialize() => WriteLines("UnitTest2 - TestInitialize");

            [TestCleanup]
            public void TestCleanup() => WriteLines("UnitTest2 - TestCleanup");

            [ClassCleanup]
            public static void ClassCleanup() => WriteLines("UnitTest2 - ClassCleanup");

            [TestMethod]
            public async Task TestMethod1() => await WriteTestOutputAsync("UnitTest2 - TestMethod1");

            [TestMethod]
            public async Task TestMethod2() => await WriteTestOutputAsync("UnitTest2 - TestMethod2");

            [TestMethod]
            public async Task TestMethod3() => await WriteTestOutputAsync("UnitTest2 - TestMethod3");

            private static async Task WriteTestOutputAsync(string methodName)
            {
                WriteLines($"{methodName} - Call 1");
                await Task.Delay(Rng.Next(20, 50));
                WriteLines($"{methodName} - Call 2");
                await Task.Delay(Rng.Next(20, 50));
                WriteLines($"{methodName} - Call 3");
            }

            private static void WriteLines(string message)
            {
                Trace.WriteLine(message);
                Console.WriteLine(message);
                Console.Error.WriteLine(message);
            }
        }
        """;
}
