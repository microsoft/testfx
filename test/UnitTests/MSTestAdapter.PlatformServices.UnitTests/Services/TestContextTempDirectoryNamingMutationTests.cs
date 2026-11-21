// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace MSTestAdapter.PlatformServices.UnitTests.Services;

public sealed class TestContextTempDirectoryNamingMutationTests : TestContainer
{
    private const string RetainVariable = "MSTEST_TEST_TEMP_DIRECTORY_RETAIN";
    private readonly string? _previousRetain = Environment.GetEnvironmentVariable(RetainVariable);
    private readonly IPlatformServiceProvider _previousPlatform = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _platform = new();

    public TestContextTempDirectoryNamingMutationTests()
    {
        Environment.SetEnvironmentVariable(RetainVariable, null);
        PlatformServiceProvider.Instance = _platform;
    }

    public void ReadableNameBudgetReservesHeadroomSeparatorsAndUniqueSuffix()
    {
        // 260 (MAX_PATH) - 80 (headroom) - base - 1 (separator) - 1 (underscore) - 32 (unique suffix).
        ComputeReadableNameBudget(new string('x', 126)).Should().Be(20);
        ComputeReadableNameBudget(new string('x', 146)).Should().Be(0);
        ComputeReadableNameBudget(new string('x', 150)).Should().Be(-4);
    }

    public void SanitizeCollapsesEveryWhitespaceRunIntoASingleUnderscore()
    {
        Sanitize("a b", 50).Should().Be("a_b");
        Sanitize("a   b", 50).Should().Be("a_b");
        Sanitize("a \t\r\n b c", 50).Should().Be("a_b_c");
        Sanitize("  lead and trail  ", 50).Should().Be("lead_and_trail");
        Sanitize("a_ b", 50).Should().Be("a__b");
    }

    public void SanitizeReplacesControlAndReservedCharacters()
    {
        Sanitize("a\u0001b", 50).Should().Be("a_b");
        Sanitize("a\u007Fb", 50).Should().Be("a_b");
        Sanitize("a<b", 50).Should().Be("a_b");
        Sanitize("a<b>c:d\"e/f\\g|h?i*j", 50).Should().Be("a_b_c_d_e_f_g_h_i_j");
    }

    public void SanitizeReturnsEmptyWhenThereIsNoNameOrNoBudget()
    {
        Sanitize(null, 50).Should().BeEmpty();
        Sanitize(string.Empty, 50).Should().BeEmpty();
        Sanitize("Scratch", 0).Should().BeEmpty();
        Sanitize("Scratch", -1).Should().BeEmpty();
    }

    public void SanitizeTruncatesToTheBudgetWithoutSplittingSurrogatePairs()
    {
        Sanitize(new string('a', 51), 50).Should().Be(new string('a', 50));
        Sanitize(new string('a', 50), 50).Should().Be(new string('a', 50));
        Sanitize(new string('a', 49) + "\uD83D", 50).Should().Be(new string('a', 49) + "\uD83D");
        Sanitize(new string('a', 49) + "\U0001F600xyz", 50).Should().Be(new string('a', 49));
        Sanitize("abc defgh", 4).Should().Be("abc");
    }

    public void TestTempDirectoryNameStartsWithTheDisplayNameWhenOneIsSet()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        context.SetDisplayName("My name");

        Path.GetFileName(context.TestTempDirectory).Should().MatchRegex("^My_name_[0-9a-f]{32}$");
    }

    public void TestTempDirectoryNameStartsWithTheTestNameWithoutADisplayName()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        context.SetDisplayName(string.Empty);

        Path.GetFileName(context.TestTempDirectory).Should().MatchRegex("^Scratch_[0-9a-f]{32}$");
    }

    public void TestTempDirectoryNameIsOnlyTheUniqueSuffixWhenNothingReadableRemains()
    {
        using ContextDirectory directory = new();
        using TestContextImplementation context = CreateContext(directory.Path);
        context.SetDisplayName("<>:*?");

        Path.GetFileName(context.TestTempDirectory).Should().MatchRegex("^[0-9a-f]{32}$");
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            Environment.SetEnvironmentVariable(RetainVariable, _previousRetain);
            PlatformServiceProvider.Instance = _previousPlatform;
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static string Sanitize(string? name, int maxLength)
        => (string)typeof(TestContextImplementation)
            .GetMethod("SanitizeTestTempDirectoryName", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [name, maxLength])!;

    private static int ComputeReadableNameBudget(string baseDirectory)
        => (int)typeof(TestContextImplementation)
            .GetMethod("ComputeReadableNameBudget", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [baseDirectory])!;

    private static TestContextImplementation CreateContext(string results)
    {
        var method = new Mock<ITestMethod>();
        method.SetupGet(value => value.Name).Returns("Scratch");
        var context = new TestContextImplementation(
            method.Object, "Tests.Scratch", new Dictionary<string, object?> { ["TestResultsDirectory"] = results }, null, null);
        context.SetOutcome(UnitTestOutcome.Passed);
        return context;
    }

    private sealed class ContextDirectory : IDisposable
    {
        public ContextDirectory() => Path = Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "t" + Guid.NewGuid().ToString("N").Substring(0, 12))).FullName;

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
#endif
