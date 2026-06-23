// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// This attribute is used to conditionally control whether a test class or a test method will run or be ignored,
/// based on whether a given executable (tool) is available.
/// </summary>
/// <remarks>
/// <para>
/// This is a generic, tool-agnostic condition. There are two modes:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     When no <see cref="Arguments"/> are supplied, the condition only checks that the executable is
///     <em>discoverable</em> on the system <c>PATH</c> (and, on Windows, resolvable through <c>PATHEXT</c>). It does
///     not run the executable. For example, it can verify that <c>docker</c> is installed.
///     </description>
///   </item>
///   <item>
///     <description>
///     When <see cref="Arguments"/> are supplied, the condition runs <c>executable arguments</c> and is met only when
///     the process exits with code <c>0</c> within <see cref="TimeoutSeconds"/>. For example,
///     <c>[ExecutableCondition("docker", "version")]</c> verifies that the Docker CLI actually responds. The process
///     output is redirected and discarded, and the process tree is terminated if it exceeds the timeout.
///     </description>
///   </item>
/// </list>
/// <para>
/// Product-specific state (for example whether the Docker daemon is configured for Linux containers) is intentionally
/// out of scope; layer such checks on top of this attribute or author your own <see cref="ConditionBaseAttribute"/>.
/// </para>
/// <para>
/// Each distinct executable-and-arguments combination forms its own condition group (see <see cref="GroupName"/>).
/// As a result, applying the attribute multiple times with different commands requires <em>all</em> of them to be
/// satisfied (logical AND), while applying it multiple times with the same command requires only one to match
/// (logical OR).
/// </para>
/// <para>
/// This attribute isn't inherited. Applying it to a base class will not affect derived classes.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class ExecutableConditionAttribute : ConditionBaseAttribute
{
    // The availability of a given command doesn't change during a test run, so the (potentially repeated) probe is
    // cached. Keyed by the executable and arguments so distinct commands are resolved independently.
    private static readonly ConcurrentDictionary<string, bool> ResultCache = new(StringComparer.Ordinal);

#if !NET
    private static readonly char[] QuoteOrWhitespace = [' ', '\t', '\n', '\v', '"'];
#endif

    private readonly string[] _arguments;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableConditionAttribute"/> class that includes the test only
    /// when the given executable is available on <c>PATH</c>.
    /// </summary>
    /// <param name="executable">
    /// The executable to look for. This is typically a bare command name (for example <c>docker</c>), but a path
    /// containing a directory separator is also supported, in which case it is checked directly instead of being
    /// resolved against <c>PATH</c>.
    /// </param>
    public ExecutableConditionAttribute(string executable)
        : this(ConditionMode.Include, executable, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableConditionAttribute"/> class that checks whether the given
    /// executable is available on <c>PATH</c>.
    /// </summary>
    /// <param name="mode">Decides whether the test is included or excluded when the executable is available.</param>
    /// <param name="executable">
    /// The executable to look for. This is typically a bare command name (for example <c>docker</c>), but a path
    /// containing a directory separator is also supported, in which case it is checked directly instead of being
    /// resolved against <c>PATH</c>.
    /// </param>
    public ExecutableConditionAttribute(ConditionMode mode, string executable)
        : this(mode, executable, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableConditionAttribute"/> class that includes the test only
    /// when the given command is available.
    /// </summary>
    /// <param name="executable">
    /// The executable to look for. This is typically a bare command name (for example <c>docker</c>), but a path
    /// containing a directory separator is also supported, in which case it is checked directly instead of being
    /// resolved against <c>PATH</c>.
    /// </param>
    /// <param name="arguments">
    /// Optional arguments. When omitted, only the presence of <paramref name="executable"/> on <c>PATH</c> is checked.
    /// When provided, <c>executable arguments</c> is executed and the condition is met only if it exits with code 0.
    /// </param>
    public ExecutableConditionAttribute(string executable, params string[] arguments)
        : this(ConditionMode.Include, executable, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableConditionAttribute"/> class.
    /// </summary>
    /// <param name="mode">Decides whether the test is included or excluded when the command is available.</param>
    /// <param name="executable">
    /// The executable to look for. This is typically a bare command name (for example <c>docker</c>), but a path
    /// containing a directory separator is also supported, in which case it is checked directly instead of being
    /// resolved against <c>PATH</c>.
    /// </param>
    /// <param name="arguments">
    /// Optional arguments. When omitted, only the presence of <paramref name="executable"/> on <c>PATH</c> is checked.
    /// When provided, <c>executable arguments</c> is executed and the condition is met only if it exits with code 0.
    /// </param>
    public ExecutableConditionAttribute(ConditionMode mode, string executable, params string[] arguments)
        : base(mode)
    {
        Executable = executable ?? throw new ArgumentNullException(nameof(executable));
        _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));

        string predicate = _arguments.Length == 0
            ? $"executable '{executable}' is available on PATH"
            : $"command '{executable} {string.Join(" ", _arguments)}' succeeds";
        IgnoreMessage = mode == ConditionMode.Include
            ? $"Test is only supported when {predicate}"
            : $"Test is not supported when {predicate}";
    }

    /// <summary>
    /// Gets the executable that this condition looks for.
    /// </summary>
    public string Executable { get; }

    /// <summary>
    /// Gets the arguments passed to the executable. When empty, only the presence of the executable on <c>PATH</c> is
    /// checked; otherwise the executable is run with these arguments and must exit with code 0.
    /// </summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>
    /// Gets or sets the maximum number of seconds to wait for the executable to exit when <see cref="Arguments"/> are
    /// provided. A value less than or equal to 0 means no timeout. The default is 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <inheritdoc />
    public override bool IsConditionMet
        => ResultCache.GetOrAdd(GroupName, _ => Evaluate());

    /// <summary>
    /// Gets the group name for this attribute. Each command forms its own group so that requiring several different
    /// commands is combined with a logical AND.
    /// </summary>
    public override string GroupName
        => _arguments.Length == 0
            ? $"ExecutableCondition:{Executable}"
            : $"ExecutableCondition:{Executable} {string.Join(" ", _arguments)}";

    private static bool IsWindows =>
#if NET462
        // The net462 reference assemblies don't expose RuntimeInformation, and that target only runs on Windows.
        true;
#else
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif

    private bool Evaluate()
        => _arguments.Length == 0
            ? ExecutableExistsOnPath(Executable)
            : TryRunWithExitCodeZero();

    private bool TryRunWithExitCodeZero()
    {
        try
        {
            var startInfo = new ProcessStartInfo(Executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

#if NET
            foreach (string argument in _arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
#else
            // ProcessStartInfo.ArgumentList isn't available on these targets, so build a quoted argument string.
            startInfo.Arguments = PasteArguments(_arguments);
#endif

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            // Drain the redirected streams asynchronously so a chatty process can't fill the pipe buffer and deadlock.
            process.OutputDataReceived += static (_, _) => { };
            process.ErrorDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            int timeoutMilliseconds = TimeoutSeconds <= 0 ? Timeout.Infinite : TimeoutSeconds * 1000;
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try
                {
#if NET
                    process.Kill(entireProcessTree: true);
#else
                    process.Kill();
#endif
                }
                catch
                {
                    // Best effort: nothing more we can do if the process can't be terminated.
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            // A missing executable, permission issue, or any other failure means the condition isn't met.
            return false;
        }
    }

    private static bool ExecutableExistsOnPath(string executable)
    {
        if (executable.Length == 0)
        {
            return false;
        }

        bool isWindows = IsWindows;
        string[] extensions = GetExecutableExtensions(executable, isWindows);

        // A value that contains a directory component is treated as a path and checked directly rather than being
        // resolved against PATH.
        if (executable.IndexOf(Path.DirectorySeparatorChar) >= 0
            || executable.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            return MatchesAnyExtension(executable, extensions);
        }

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is null)
        {
            return false;
        }

        foreach (string directory in pathVariable.Split(Path.PathSeparator))
        {
            if (directory.Length == 0)
            {
                continue;
            }

            string candidate;
            try
            {
                candidate = Path.Combine(directory, executable);
            }
            catch (ArgumentException)
            {
                // Skip malformed PATH entries that contain invalid path characters.
                continue;
            }

            if (MatchesAnyExtension(candidate, extensions))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAnyExtension(string path, string[] extensions)
    {
        foreach (string extension in extensions)
        {
            if (File.Exists(path + extension))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetExecutableExtensions(string executable, bool isWindows)
    {
        // On non-Windows platforms (or when the caller already specified an extension) only an exact match is relevant.
        if (!isWindows || Path.HasExtension(executable))
        {
            return [string.Empty];
        }

        string? pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrEmpty(pathExt))
        {
            return [string.Empty, ".exe", ".cmd", ".bat", ".com"];
        }

        // PATHEXT entries already include the leading dot, for example ".COM;.EXE;.BAT".
        string[] parts = pathExt!.Split([Path.PathSeparator], StringSplitOptions.RemoveEmptyEntries);
        string[] result = new string[parts.Length + 1];

        // Keep an empty extension first so an exact match (for example when a full file name was passed) still wins.
        result[0] = string.Empty;
        Array.Copy(parts, 0, result, 1, parts.Length);
        return result;
    }

#if !NET
    // Mirrors the argument-quoting performed by ProcessStartInfo.ArgumentList on modern .NET (the well-known
    // PasteArguments algorithm), used only on targets where ArgumentList isn't available.
    private static string PasteArguments(string[] arguments)
    {
        var builder = new StringBuilder();
        foreach (string argument in arguments)
        {
            if (builder.Length != 0)
            {
                builder.Append(' ');
            }

            if (argument.Length != 0 && argument.IndexOfAny(QuoteOrWhitespace) < 0)
            {
                builder.Append(argument);
                continue;
            }

            builder.Append('"');
            int index = 0;
            while (index < argument.Length)
            {
                char c = argument[index++];
                if (c == '\\')
                {
                    int backslashCount = 1;
                    while (index < argument.Length && argument[index] == '\\')
                    {
                        backslashCount++;
                        index++;
                    }

                    if (index == argument.Length)
                    {
                        builder.Append('\\', backslashCount * 2);
                    }
                    else if (argument[index] == '"')
                    {
                        builder.Append('\\', (backslashCount * 2) + 1);
                        builder.Append('"');
                        index++;
                    }
                    else
                    {
                        builder.Append('\\', backslashCount);
                    }
                }
                else if (c == '"')
                {
                    builder.Append('\\');
                    builder.Append('"');
                }
                else
                {
                    builder.Append(c);
                }
            }

            builder.Append('"');
        }

        return builder.ToString();
    }
#endif
}
