// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextAssertionFailureDiagnosticsWriterPass2MutationTests : TestContainer
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Type ActiveTestType = GetNestedType("ActiveTest");
    private static readonly Type ResourceBaselineType = GetNestedType("ResourceBaseline");
    private static readonly MethodInfo WriteMethod = GetStaticMethod("WriteAssertionFailureDiagnostics");
    private static readonly IDictionary ActiveTests = (IDictionary)typeof(TestContextImplementation).GetField("ActiveTests", StaticMembers)!.GetValue(null)!;

    public void ActiveTestsAreWrittenWithTheirStartInstantAsAnExactRoundTripTimestamp()
    {
        using DiagnosticsDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        DateTimeOffset failingStart = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero).AddTicks(1_234_567);
        var otherStart = new DateTimeOffset(2025, 12, 31, 23, 59, 58, TimeSpan.Zero);
        object failing = CreateActiveTest(context, -2_000_001, "Tests.StartInstantFailing", failingStart);
        object other = CreateActiveTest(context, -2_000_002, "Tests.StartInstantOther", otherStart);

        using (RegisterActiveTests(failing, other))
        using (JsonDocument artifact = WriteArtifact(failing, "message", null, null))
        {
            var startedAtByTest = artifact.RootElement.GetProperty("activeTests").EnumerateArray()
                .ToDictionary(value => value.GetProperty("fullyQualifiedName").GetString()!, value => value.GetProperty("startedAtUtc").GetString()!);

            startedAtByTest["Tests.StartInstantFailing.Runs"].Should().Be("2026-01-02T03:04:05.1234567+00:00");
            startedAtByTest["Tests.StartInstantOther.Runs"].Should().Be("2025-12-31T23:59:58.0000000+00:00");
            DateTimeOffset.ParseExact(startedAtByTest["Tests.StartInstantFailing.Runs"], "O", CultureInfo.InvariantCulture).Should().Be(failingStart);
        }
    }

    private static Type GetNestedType(string name)
        => typeof(TestContextImplementation).GetNestedType(name, BindingFlags.NonPublic | BindingFlags.Public)!;

    private static MethodInfo GetStaticMethod(string name)
        => typeof(TestContextImplementation).GetMethod(name, StaticMembers)!;

    private static T GetValue<T>(object instance, string propertyName)
        => (T)instance.GetType().GetProperty(propertyName, InstanceMembers)!.GetValue(instance)!;

    private static TestContextImplementation CreateContext(string resultsDirectory)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.FullClassName).Returns("Tests.Writer");
        method.SetupGet(value => value.Name).Returns("Runs");
        return new TestContextImplementation(
            method.Object, "Tests.Writer", new Dictionary<string, object?> { ["TestResultsDirectory"] = resultsDirectory }, null, null);
    }

    private static object CreateActiveTest(TestContextImplementation context, long id, string className, DateTimeOffset startedAtUtc)
        => Activator.CreateInstance(
            ActiveTestType,
            InstanceMembers,
            null,
            [id, context, className, "Runs", null, 1, startedAtUtc, Stopwatch.GetTimestamp(), CreateBaseline(TimeSpan.Zero, null, null)],
            null)!;

    private static object CreateBaseline(TimeSpan processorTime, long? readBytes, long? writeBytes)
        => Activator.CreateInstance(ResourceBaselineType, processorTime, readBytes, writeBytes)!;

    private static JsonDocument WriteArtifact(object activeTest, string message, string? expected, string? actual)
    {
        string path = (string)WriteMethod.Invoke(null, [activeTest, 1, message, expected, actual])!;
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static ActiveTestRegistration RegisterActiveTests(params object[] activeTests)
        => new(activeTests);

    private sealed class ActiveTestRegistration : IDisposable
    {
        private readonly List<long> _ids = [];

        public ActiveTestRegistration(object[] activeTests)
        {
            try
            {
                foreach (object activeTest in activeTests)
                {
                    long id = GetValue<long>(activeTest, "Id");
                    ActiveTests.Add(id, activeTest);
                    _ids.Add(id);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            foreach (long id in _ids)
            {
                ActiveTests.Remove(id);
            }

            _ids.Clear();
        }
    }

    private sealed class DiagnosticsDirectory : IDisposable
    {
        public DiagnosticsDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "d" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
