// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Text;

using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace MSTest.Acceptance.IntegrationTests;

internal static class AdapterTestHost
{
    private const string TestCasePrefix = "##MSTEST-TESTCASE##";
    private const string TestResultPrefix = "##MSTEST-TESTRESULT##";

    private static readonly TestProperty HierarchyProperty = TestProperty.Register(
        id: "TestCase.Hierarchy",
        label: "Hierarchy",
        category: string.Empty,
        description: string.Empty,
        valueType: typeof(string[]),
        validateValueCallback: null,
        attributes: TestPropertyAttributes.Hidden,
        owner: typeof(TestCase));

    internal static ImmutableArray<TestCase> DiscoverTests(string assemblyPath, string? testCaseFilter = null)
    {
        TestHostResult result = Execute(assemblyPath, $"--adapter-discover {EncodeArgument(testCaseFilter)}");
        return result.StandardOutputLines
            .Where(line => line.StartsWith(TestCasePrefix, StringComparison.Ordinal))
            .Select(line => DeserializeTestCase(line[TestCasePrefix.Length..]))
            .ToImmutableArray();
    }

    internal static ImmutableArray<TestResult> RunTests(
        string assemblyPath,
        string? filterString = null,
        bool simulateOutOfProcessTestCase = false)
        => RunTests(DiscoverTests(assemblyPath, filterString), filterString, simulateOutOfProcessTestCase);

    internal static Task<ImmutableArray<TestResult>> RunTestsAsync(
        string assemblyPath,
        string? filterString = null,
        bool simulateOutOfProcessTestCase = false)
        => Task.FromResult(RunTests(assemblyPath, filterString, simulateOutOfProcessTestCase));

    internal static ImmutableArray<TestResult> RunTests(
        IEnumerable<TestCase> testCases,
        string? filterString = null,
        bool simulateOutOfProcessTestCase = false)
    {
        var materializedTestCases = testCases.ToImmutableArray();
        string assemblyPath = materializedTestCases.Select(testCase => testCase.Source).Distinct(StringComparer.OrdinalIgnoreCase).Single();
        string testIds = string.Join(";", materializedTestCases.Select(testCase => testCase.Id.ToString("D")));
        TestHostResult result = Execute(
            assemblyPath,
            $"--adapter-run {EncodeArgument(testIds)} {EncodeArgument(filterString)} {(simulateOutOfProcessTestCase ? "1" : "0")}");

        return result.StandardOutputLines
            .Where(line => line.StartsWith(TestResultPrefix, StringComparison.Ordinal))
            .Select(line => DeserializeTestResult(line[TestResultPrefix.Length..]))
            .ToImmutableArray();
    }

    internal static Task<ImmutableArray<TestResult>> RunTestsAsync(
        IEnumerable<TestCase> testCases,
        string? filterString = null,
        bool simulateOutOfProcessTestCase = false)
        => Task.FromResult(RunTests(testCases, filterString, simulateOutOfProcessTestCase));

    private static TestHostResult Execute(string assemblyPath, string arguments)
    {
        var testHost = TestHost.LocateFrom(
            Path.GetDirectoryName(assemblyPath)!,
            Path.GetFileNameWithoutExtension(assemblyPath));
        TestHostResult result = testHost.ExecuteAsync(arguments).GetAwaiter().GetResult();

        return result.ExitCode == 0
            ? result
            : throw new InvalidOperationException(result.ToString());
    }

    private static string EncodeArgument(string? value)
        => value is null ? "~" : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static TestCase DeserializeTestCase(string payload)
    {
        using var stream = new MemoryStream(Convert.FromBase64String(payload));
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        return ReadTestCase(reader);
    }

    private static TestResult DeserializeTestResult(string payload)
    {
        using var stream = new MemoryStream(Convert.FromBase64String(payload));
        using var reader = new BinaryReader(stream, Encoding.UTF8);

        TestCase testCase = ReadTestCase(reader);
        var testResult = new TestResult(testCase)
        {
            Outcome = (TestOutcome)reader.ReadInt32(),
            DisplayName = ReadNullableString(reader),
            ErrorMessage = ReadNullableString(reader),
            ErrorStackTrace = ReadNullableString(reader),
            Duration = TimeSpan.FromTicks(reader.ReadInt64()),
            StartTime = ReadDateTimeOffset(reader),
            EndTime = ReadDateTimeOffset(reader),
        };

        int messageCount = reader.ReadInt32();
        for (int i = 0; i < messageCount; i++)
        {
            testResult.Messages.Add(new TestResultMessage(reader.ReadString(), reader.ReadString()));
        }

        return testResult;
    }

    private static TestCase ReadTestCase(BinaryReader reader)
    {
        var testCase = new TestCase(reader.ReadString(), new Uri(reader.ReadString()), reader.ReadString())
        {
            DisplayName = reader.ReadString(),
            Id = new Guid(reader.ReadBytes(16)),
            CodeFilePath = ReadNullableString(reader),
            LineNumber = reader.ReadInt32(),
        };

        int hierarchyLength = reader.ReadInt32();
        if (hierarchyLength >= 0)
        {
            string?[] hierarchy = new string?[hierarchyLength];
            for (int i = 0; i < hierarchy.Length; i++)
            {
                hierarchy[i] = ReadNullableString(reader);
            }

            testCase.SetPropertyValue(HierarchyProperty, hierarchy);
        }

        int traitCount = reader.ReadInt32();
        for (int i = 0; i < traitCount; i++)
        {
            testCase.Traits.Add(new Trait(reader.ReadString(), reader.ReadString()));
        }

        return testCase;
    }

    private static string? ReadNullableString(BinaryReader reader)
        => reader.ReadBoolean() ? reader.ReadString() : null;

    private static DateTimeOffset ReadDateTimeOffset(BinaryReader reader)
        => new(reader.ReadInt64(), TimeSpan.FromTicks(reader.ReadInt64()));
}
