// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
        ArgumentGuard.IsNotNullOrEmpty(template);

        var defaultReplacements = GetDefaultReplacements();
        var allReplacements = MergeReplacements(defaultReplacements, customReplacements);

        return TemplateFieldRegex.Replace(template, match =>
        {
            string fieldName = match.Groups[1].Value;
            return allReplacements.TryGetValue(fieldName, out string? value) ? value : match.Value;
        });
    }

    public string ResolveTemplateWithLegacySupport(string template, IDictionary<string, string>? customReplacements = null, IDictionary<string, string>? legacyReplacements = null)
    {
        ArgumentGuard.IsNotNullOrEmpty(template);

        // First apply legacy replacements
        string processedTemplate = template;
        if (legacyReplacements is not null)
        {
            foreach (var (legacyPattern, replacement) in legacyReplacements)
            {
                processedTemplate = processedTemplate.Replace(legacyPattern, replacement);
            }
        }

        // Then apply modern template resolution
        return ResolveTemplate(processedTemplate, customReplacements);
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
        using var currentProcess = _processHandler.GetCurrentProcess();
        replacements["pid"] = currentProcess.Id.ToString(CultureInfo.InvariantCulture);
        replacements["pname"] = currentProcess.ProcessName;

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
        foreach (var (key, value) in customReplacements)
        {
            merged[key] = value;
        }

        return merged;
    }

    private static string GetOperatingSystemName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macos";
        }

        return "unknown";
    }

    private static string GetTargetFrameworkMoniker()
    {
        // Extract TFM from current runtime
        string frameworkDescription = RuntimeInformation.FrameworkDescription;
        
        if (frameworkDescription.Contains(".NET Core"))
        {
            // Try to extract version from .NET Core description
            var match = Regex.Match(frameworkDescription, @"\.NET Core (\d+\.\d+)");
            if (match.Success)
            {
                return $"netcoreapp{match.Groups[1].Value}";
            }
        }
        else if (frameworkDescription.Contains(".NET "))
        {
            // Try to extract version from .NET 5+ description
            var match = Regex.Match(frameworkDescription, @"\.NET (\d+\.\d+)");
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
                    _ => $"net{version}"
                };
            }
        }
        else if (frameworkDescription.Contains(".NET Framework"))
        {
            // Try to extract version from .NET Framework description
            var match = Regex.Match(frameworkDescription, @"\.NET Framework (\d+\.\d+)");
            if (match.Success)
            {
                return $"net{match.Groups[1].Value.Replace(".", "")}";
            }
        }

        return Environment.Version.ToString();
    }

    private static string GenerateShortId()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }

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