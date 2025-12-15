// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Services;

internal sealed class ArtifactNamingService : IArtifactNamingService
{
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IEnvironment _environment;
    private readonly IClock _clock;
    private readonly IProcessHandler _processHandler;

    private static readonly Regex TemplateFieldRegex = new(@"<([^>]+)>", RegexOptions.Compiled);

    public ArtifactNamingService(
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IEnvironment environment,
        IClock clock,
        IProcessHandler processHandler)
    {
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _environment = environment;
        _clock = clock;
        _processHandler = processHandler;
    }

    public string ResolveTemplate(string template, IDictionary<string, string>? customReplacements = null)
    {
        Guard.NotNullOrEmpty(template);

        Dictionary<string, string> defaultReplacements = GetDefaultReplacements();
        Dictionary<string, string> allReplacements = MergeReplacements(defaultReplacements, customReplacements);

        return TemplateFieldRegex.Replace(template, match =>
        {
            string fieldName = match.Groups[1].Value;
            return allReplacements.TryGetValue(fieldName, out string? value) ? value : match.Value;
        });
    }

    private Dictionary<string, string> GetDefaultReplacements()
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Assembly info
        string? assemblyName = _testApplicationModuleInfo.TryGetAssemblyName();
        if (!RoslynString.IsNullOrEmpty(assemblyName))
        {
            replacements["asm"] = assemblyName;
        }

        // Process info
        using IProcess currentProcess = _processHandler.GetCurrentProcess();
        replacements["pid"] = currentProcess.Id.ToString(CultureInfo.InvariantCulture);
        replacements["pname"] = currentProcess.Name;

        // OS info
        replacements["os"] = GetOperatingSystemName();

        // Target framework info
        string tfm = GetTargetFrameworkMoniker();
        if (!RoslynString.IsNullOrEmpty(tfm))
        {
            replacements["tfm"] = tfm;
        }

        // Time info (precision to 1 second)
        replacements["time"] = _clock.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        // Random ID for uniqueness
        replacements["id"] = GenerateShortId();

        // Root directory
        replacements["root"] = GetRootDirectory();

        return replacements;
    }

    private static Dictionary<string, string> MergeReplacements(Dictionary<string, string> defaultReplacements, IDictionary<string, string>? customReplacements)
    {
        if (customReplacements is null || customReplacements.Count == 0)
        {
            return defaultReplacements;
        }

        var merged = new Dictionary<string, string>(defaultReplacements, StringComparer.OrdinalIgnoreCase);
        foreach ((string? key, string? value) in customReplacements)
        {
            merged[key] = value;
        }

        return merged;
    }

    private static string GetOperatingSystemName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macos";
        }

        // Fallback for unknown OS
        return "unknown";
    }

    private string GetTargetFrameworkMoniker()
    {
        // Extract TFM from current runtime
        string frameworkDescription = RuntimeInformation.FrameworkDescription;

        if (frameworkDescription.Contains(".NET Core"))
        {
            // Try to extract version from .NET Core description
            Match match = Regex.Match(frameworkDescription, @"\.NET Core (\d+\.\d+)");
            if (match.Success)
            {
                return $"netcoreapp{match.Groups[1].Value}";
            }
        }
        else if (frameworkDescription.Contains(".NET "))
        {
            // Try to extract version from .NET 5+ description
            Match match = Regex.Match(frameworkDescription, @"\.NET (\d+\.\d+)");
            if (match.Success)
            {
                string version = match.Groups[1].Value;
                return version switch
                {
                    "5.0" => "net5.0",
                    "6.0" => "net6.0",
                    "7.0" => "net7.0",
                    "8.0" => "net8.0",
                    "9.0" => "net9.0",
                    "10.0" => "net10.0",
                    _ => $"net{version}",
                };
            }
        }
        else if (frameworkDescription.Contains(".NET Framework"))
        {
            // Try to extract version from .NET Framework description
            Match match = Regex.Match(frameworkDescription, @"\.NET Framework (\d+\.\d+)");
            if (match.Success)
            {
                return $"net{match.Groups[1].Value.Replace(".", string.Empty)}";
            }
        }

        return _environment.Version.ToString();
    }

    private static string GenerateShortId()
        => Guid.NewGuid().ToString("N")[..8];

    private string GetRootDirectory()
    {
        string currentDirectory = _testApplicationModuleInfo.GetCurrentTestApplicationDirectory();

        // Try to find solution root, git root, or working directory
        string? rootDirectory = FindSolutionRoot(currentDirectory)
            ?? FindGitRoot(currentDirectory)
            ?? currentDirectory;

        return rootDirectory;
    }

    private static string? FindSolutionRoot(string startDirectory)
    {
        string? directory = startDirectory;
        while (directory is not null)
        {
            if (Directory.GetFiles(directory, "*.sln").Length > 0)
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }

    private static string? FindGitRoot(string startDirectory)
    {
        string? directory = startDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return null;
    }
}
