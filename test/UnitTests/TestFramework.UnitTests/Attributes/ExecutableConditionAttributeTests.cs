// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class ExecutableConditionAttribute.
/// </summary>
public class ExecutableConditionAttributeTests : TestContainer
{
    public void Constructor_SetsCorrectMode()
    {
        var includeAttribute = new ExecutableConditionAttribute(ConditionMode.Include, "docker");
        var excludeAttribute = new ExecutableConditionAttribute(ConditionMode.Exclude, "docker");

        includeAttribute.Mode.Should().Be(ConditionMode.Include);
        excludeAttribute.Mode.Should().Be(ConditionMode.Exclude);
    }

    public void Constructor_SingleArgument_DefaultsToIncludeMode()
    {
        var attribute = new ExecutableConditionAttribute("docker");

        attribute.Mode.Should().Be(ConditionMode.Include);
    }

    public void Constructor_NullExecutable_Throws()
    {
        Action act = () => _ = new ExecutableConditionAttribute(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    public void Executable_ReturnsProvidedValue()
        => new ExecutableConditionAttribute("docker").Executable.Should().Be("docker");

    public void GroupName_IncludesExecutable()
        => new ExecutableConditionAttribute("docker").GroupName.Should().Be("ExecutableCondition:presence\0docker\0Include");

    public void GroupName_IncludesArguments()
        => new ExecutableConditionAttribute("docker", "version").GroupName
            .Should().Be("ExecutableCondition:run\0docker\0version\0Include");

    // A presence check for an executable literally named "foo bar" must not collide with running "foo" with the
    // argument "bar"; the null-character separator keeps the two distinct.
    public void GroupName_PresenceAndRunDoNotCollide()
        => new ExecutableConditionAttribute("foo bar").GroupName
            .Should().NotBe(new ExecutableConditionAttribute("foo", "bar").GroupName);

    // Include and Exclude for the same command must NOT share a GroupName, otherwise the two would land in the same
    // OR group and silently cancel each other out instead of being AND-ed.
    public void GroupName_DiffersByMode()
        => new ExecutableConditionAttribute(ConditionMode.Include, "docker").GroupName
            .Should().NotBe(new ExecutableConditionAttribute(ConditionMode.Exclude, "docker").GroupName);

    // The attribute must be immutable: mutating the array the caller passed must not change Arguments.
    public void Arguments_AreCopiedFromConstructorArray()
    {
        string[] args = ["version", "--format"];
        var attribute = new ExecutableConditionAttribute("docker", args);
        args[0] = "mutated";

        attribute.Arguments.Should().Equal("version", "--format");
    }

    public void Arguments_DefaultsToEmpty()
        => new ExecutableConditionAttribute("docker").Arguments.Should().BeEmpty();

    public void Arguments_ReturnsProvidedValues()
        => new ExecutableConditionAttribute("docker", "version", "--format", "json").Arguments
            .Should().Equal("version", "--format", "json");

    public void TimeoutSeconds_DefaultsTo30()
        => new ExecutableConditionAttribute("docker", "version").TimeoutSeconds.Should().Be(30);

    // A different GroupName per executable is what makes MSTest combine the conditions with a logical AND,
    // so requiring two different tools requires both of them to be available.
    public void GroupName_DiffersBetweenExecutables()
        => new ExecutableConditionAttribute("docker").GroupName
            .Should().NotBe(new ExecutableConditionAttribute("git").GroupName);

    public void IgnoreMessage_IncludeMode_ReturnsCorrectMessage()
        => new ExecutableConditionAttribute(ConditionMode.Include, "docker").IgnoreMessage
            .Should().Be("Test is only supported when executable 'docker' is available on PATH");

    public void IgnoreMessage_ExcludeMode_ReturnsCorrectMessage()
        => new ExecutableConditionAttribute(ConditionMode.Exclude, "docker").IgnoreMessage
            .Should().Be("Test is not supported when executable 'docker' is available on PATH");

    public void IgnoreMessage_WithArguments_MentionsCommandSucceeds()
        => new ExecutableConditionAttribute(ConditionMode.Include, "docker", "version").IgnoreMessage
            .Should().Be("Test is only supported when command 'docker version' succeeds");

    public void IsConditionMet_WhenExecutableMissing_ReturnsFalse()
    {
        // A randomized name that cannot exist on PATH (and is not cached from another test).
        var attribute = new ExecutableConditionAttribute($"definitely-not-a-real-tool-{Guid.NewGuid():N}");

        attribute.IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_WhenExecutableOnPath_ReturnsTrue()
    {
        string directory = CreateTemporaryDirectory();
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        // Use an extension that resolves on each platform: PATHEXT on Windows, a bare file elsewhere.
        string commandName = $"fake-tool-{Guid.NewGuid():N}";
        string fileName = isWindows ? $"{commandName}.cmd" : commandName;
        File.WriteAllText(Path.Combine(directory, fileName), string.Empty);

        string originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        try
        {
            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + originalPath);

            new ExecutableConditionAttribute(commandName).IsConditionMet.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    public void IsConditionMet_WhenFullPathProvided_ReturnsTrue()
    {
        string directory = CreateTemporaryDirectory();
        string fullPath = Path.Combine(directory, $"fake-tool-{Guid.NewGuid():N}");
        File.WriteAllText(fullPath, string.Empty);

        new ExecutableConditionAttribute(fullPath).IsConditionMet.Should().BeTrue();
    }

    public void IsConditionMet_IsCachedPerExecutable()
    {
        string directory = CreateTemporaryDirectory();
        string commandName = $"cached-tool-{Guid.NewGuid():N}";
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string fileName = isWindows ? $"{commandName}.cmd" : commandName;
        string filePath = Path.Combine(directory, fileName);
        File.WriteAllText(filePath, string.Empty);

        string originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        try
        {
            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + originalPath);

            var attribute = new ExecutableConditionAttribute(commandName);
            attribute.IsConditionMet.Should().BeTrue();

            // Deleting the file should not change the result because the first probe is cached.
            File.Delete(filePath);
            attribute.IsConditionMet.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    public void IsConditionMet_WhenCommandExitsZero_ReturnsTrue()
    {
        (string executable, string[] arguments) = ExitCommand(0);

        new ExecutableConditionAttribute(executable, arguments).IsConditionMet.Should().BeTrue();
    }

    public void IsConditionMet_WhenCommandExitsNonZero_ReturnsFalse()
    {
        (string executable, string[] arguments) = ExitCommand(1);

        new ExecutableConditionAttribute(executable, arguments).IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_WhenExecutableMissingButArgumentsProvided_ReturnsFalse()
    {
        var attribute = new ExecutableConditionAttribute($"definitely-not-a-real-tool-{Guid.NewGuid():N}", "version");

        attribute.IsConditionMet.Should().BeFalse();
    }

    public void IsConditionMet_WhenCommandExceedsTimeout_ReturnsFalse()
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        // Invoke ping/sleep directly (no shell wrapper) so killing the root process reliably stops the long-running
        // command even on TFMs where only the root process can be killed (no entire-process-tree kill).
        ExecutableConditionAttribute attribute = isWindows
            ? new ExecutableConditionAttribute("ping", "-n", "30", "127.0.0.1") { TimeoutSeconds = 1 }
            : new ExecutableConditionAttribute("sleep", "30") { TimeoutSeconds = 1 };

        attribute.IsConditionMet.Should().BeFalse();
    }

    // Produces a per-OS command guaranteed to be on PATH that exits with the given code.
    private static (string Executable, string[] Arguments) ExitCommand(int exitCode)
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("cmd", ["/c", "exit", exitCode.ToString(CultureInfo.InvariantCulture)])
            : ("sh", ["-c", $"exit {exitCode}"]);

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }
}
