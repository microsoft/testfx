// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// The out-of-band hand-off that lets a packaged (MSIX) test host receive the controller and retry
/// environment variables that a plain <c>Process.Start</c> would have inherited.
/// </summary>
/// <remarks>
/// <para>
/// The platform tells a spawned test host how to connect back to its controller through environment
/// variables (the IPC pipe name, correlation id, parent PID, …). A packaged app activated by
/// Application User Model ID is created by the Windows activation/PLM infrastructure, not by the
/// controller, so it does <em>not</em> inherit that environment block. To bridge the gap the launcher
/// (which runs in the controller and therefore has the fully-prepared environment) writes those
/// variables to a small file inside the package's own writable data folder
/// (<c>%LOCALAPPDATA%\Packages\{PackageFamilyName}\LocalState</c>, a path both sides derive from the
/// package family name), and the activated host reads them back in-process — before the platform's
/// own environment-variable-based connect-back runs — through
/// <see cref="TestingPlatformBuilderHook"/>.
/// </para>
/// <para>
/// The file is named with a stable handshake ID so concurrent runs of the same package do not collide.
/// Controller-host runs use the controller PID; retry runs use a hash of their unique pipe name. The
/// host deletes the file as soon as it has been consumed.
/// </para>
/// </remarks>
internal static class PackagedAppConnectBackHandshake
{
    // The platform's command-line option that carries the test host controller PID. Both the launcher
    // (naming the file) and the activated host (locating it) agree on this value. Kept as a literal
    // because PlatformCommandLineProvider.TestHostControllerPIDOptionKey is internal to the platform
    // assembly; the two ship and version together in this repo.
    private const string TestHostControllerPidOptionKey = "internal-testhostcontroller-pid";
    private const string RetryPipeNameOptionKey = "internal-retry-pipename";

    // Marker prefixes distinguishing a null value from a (possibly empty) string value on each line,
    // so an empty string round-trips as an empty string rather than as null.
    private const char NullValueMarker = 'N';
    private const char StringValueMarker = 'V';

    /// <summary>
    /// Returns the directory inside the package's writable local application data where the handshake
    /// file is exchanged (<c>%LOCALAPPDATA%\Packages\{packageFamilyName}\LocalState</c>). This is used by
    /// the launcher, which runs in the (unpackaged) controller where <see cref="Environment.SpecialFolder.LocalApplicationData"/>
    /// is the real, unredirected local app data. The activated packaged host must instead resolve its own
    /// package LocalState through the package-aware <c>ApplicationData.Current.LocalFolder</c>, because in
    /// a packaged process local app data is redirected away from this path.
    /// </summary>
    public static string GetHandshakeDirectory(string packageFamilyName)
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            packageFamilyName,
            "LocalState");

    /// <summary>
    /// Returns the file name (without directory) for a handshake ID. The directory differs between the
    /// two sides (see <see cref="GetHandshakeFilePath"/> and the activated host, which resolves its own
    /// package LocalState), but the file name is shared.
    /// </summary>
    public static string GetHandshakeFileName(string handshakeId)
        => $"mtp-testhostcontroller-{handshakeId}.handshake";

    /// <summary>
    /// Returns the full path of the handshake file for a package family name and handshake ID.
    /// </summary>
    public static string GetHandshakeFilePath(string packageFamilyName, string handshakeId)
        => Path.Combine(
            GetHandshakeDirectory(packageFamilyName),
            GetHandshakeFileName(handshakeId));

    /// <summary>
    /// Extracts the value of the <c>--internal-testhostcontroller-pid</c> option from a command line,
    /// or <see langword="null"/> when it is absent (which means the current process is not an activated
    /// test host waiting on a controller connect-back).
    /// </summary>
    public static string? TryGetTestHostControllerPid(IReadOnlyList<string> arguments)
        => TryGetOptionValue(arguments, TestHostControllerPidOptionKey);

    /// <summary>
    /// Returns the stable identifier used to exchange environment variables for an activated host.
    /// Controller-host runs use the controller PID; retry-orchestrated runs use a hash of their unique pipe name.
    /// </summary>
    public static string? TryGetHandshakeId(IReadOnlyList<string> arguments)
    {
        if (TryGetTestHostControllerPid(arguments) is { } testHostControllerPid)
        {
            return testHostControllerPid;
        }

        if (TryGetOptionValue(arguments, RetryPipeNameOptionKey) is not { } retryPipeName)
        {
            return null;
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(retryPipeName));
        return $"retry-{Convert.ToHexString(hash)}";
    }

    private static string? TryGetOptionValue(IReadOnlyList<string> arguments, string optionKey)
    {
        for (int i = 0; i < arguments.Count - 1; i++)
        {
            if (string.Equals(arguments[i], $"--{optionKey}", StringComparison.Ordinal))
            {
                return arguments[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// Writes the connect-back environment variables to <paramref name="filePath"/>, creating the
    /// containing directory when needed. Overwrites any pre-existing file (for example a leftover from
    /// a crashed run) so the activated host always reads the current run's values.
    /// </summary>
    public static void Write(string filePath, IEnumerable<KeyValuePair<string, string?>> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var builder = new StringBuilder();
        foreach (KeyValuePair<string, string?> entry in entries)
        {
            builder.Append(entry.Key).Append('=');
            if (entry.Value is null)
            {
                builder.Append(NullValueMarker);
            }
            else
            {
                builder.Append(StringValueMarker).Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Value)));
            }

            builder.Append('\n');
        }

        File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Best-effort deletion of the handshake file, used to clean up when no host was activated to
    /// consume it. A failure to remove the (user-scoped, transient) file must not fail the run.
    /// </summary>
    public static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Best-effort delete of connect-back handshake file '{filePath}' failed: {ex}");
        }
    }

    /// <summary>
    /// Reads the connect-back environment variables from <paramref name="filePath"/> and deletes the
    /// file, or returns <see langword="null"/> when the file does not exist. Deletion is best-effort:
    /// a failure to remove the (user-scoped, transient) file must not fail the run.
    /// </summary>
    public static IReadOnlyDictionary<string, string?>? ReadAndDelete(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Best-effort delete of connect-back handshake file '{filePath}' failed: {ex}");
        }

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
            {
                continue;
            }

            string key = line[..separator];
            char marker = line[separator + 1];
            string encoded = line[(separator + 2)..];

            if (marker == NullValueMarker)
            {
                result[key] = null;
            }
            else if (marker == StringValueMarker)
            {
                try
                {
                    result[key] = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                }
                catch (FormatException)
                {
                    // Ignore malformed entries.
                }
            }
        }

        return result;
    }
}
