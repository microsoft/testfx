// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

using Match = System.Text.RegularExpressions.Match;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class DeploymentUtilityMutationTests : TestContainer
{
    private const string BaseDirectory = "base-directory";

    public void RootDeploymentDirectoryNameCombinesThePrefixTheUserTheTimestampAndTheProcessId()
    {
        RecordingFileUtility files = new();
        DeploymentUtility utility = CreateUtility(files);
        DateTime before = DateTime.Now;

        string root = utility.GetRootDeploymentDirectory(BaseDirectory);

        DateTime after = DateTime.Now;
        root.Should().Be(RecordingFileUtility.UniqueDirectory);
        files.ParentDirectory.Should().Be(BaseDirectory);
        Match match = Regex.Match(files.DirectoryName!, @"^Deploy_(?<user>.*) (?<stamp>\d{8}T\d{6})_(?<pid>\d+)$");
        match.Success.Should().BeTrue(because: $"'{files.DirectoryName}' should be '<prefix>_<user> <timestamp>_<process id>'");
        var stamp = DateTime.ParseExact(match.Groups["stamp"].Value, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture);
        stamp.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after);
        using var process = Process.GetCurrentProcess();
        match.Groups["pid"].Value.Should().Be(process.Id.ToString(CultureInfo.InvariantCulture));
    }

#if !NETFRAMEWORK
    public void RootDeploymentDirectoryNameUsesTheUserNameVariableBeforeTheUserVariable()
    {
        string? previousUserName = Environment.GetEnvironmentVariable("USERNAME");
        string? previousUser = Environment.GetEnvironmentVariable("USER");
        try
        {
            GetUserOfTheRootDirectoryName(userName: "alice", user: "bob").Should().Be("alice", because: "USERNAME takes precedence over USER");
            GetUserOfTheRootDirectoryName(userName: "alice", user: null).Should().Be("alice", because: "USERNAME is used when USER is not set");
            GetUserOfTheRootDirectoryName(userName: null, user: "bob").Should().Be("bob", because: "USER is used when USERNAME is not set");
            GetUserOfTheRootDirectoryName(userName: null, user: null).Should().BeEmpty(because: "no user name is available");
        }
        finally
        {
            Environment.SetEnvironmentVariable("USERNAME", previousUserName);
            Environment.SetEnvironmentVariable("USER", previousUser);
        }
    }

    private static string GetUserOfTheRootDirectoryName(string? userName, string? user)
    {
        Environment.SetEnvironmentVariable("USERNAME", userName);
        Environment.SetEnvironmentVariable("USER", user);
        RecordingFileUtility files = new();

        _ = CreateUtility(files).GetRootDeploymentDirectory(BaseDirectory);

        Match match = Regex.Match(files.DirectoryName!, @"^Deploy_(?<user>.*) \d{8}T\d{6}_\d+$");
        match.Success.Should().BeTrue(because: $"'{files.DirectoryName}' should be '<prefix>_<user> <timestamp>_<process id>'");
        return match.Groups["user"].Value;
    }
#endif

    private static DeploymentUtility CreateUtility(FileUtility files)
        => new(new DeploymentItemUtility(Mock.Of<IReflectionOperations>()), new AssemblyUtility(), files);

    private sealed class RecordingFileUtility : FileUtility
    {
        public const string UniqueDirectory = "unique-directory";

        public string? ParentDirectory { get; private set; }

        public string? DirectoryName { get; private set; }

        public override string GetNextIterationDirectoryName(string parentDirectoryName, string originalDirectoryName)
        {
            ParentDirectory = parentDirectoryName;
            DirectoryName = originalDirectoryName;
            return UniqueDirectory;
        }
    }
}
#endif
