// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Platform.Services;

internal sealed class ArtifactNamingService : IArtifactNamingService
{
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
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
        _clock = clock;
        _processHandler = processHandler;
    }

    public string ResolveTemplate(string template, IDictionary<string, string>? customReplacements = null)
    {
        if (RoslynString.IsNullOrEmpty(template))
        {
            throw new ArgumentException("Template cannot be null or empty.", nameof(template));
        }

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
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);

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

        // Target framework info (best-effort, based on runtime detection)
        string? tfm = TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
        if (!RoslynString.IsNullOrEmpty(tfm))
        {
            replacements["tfm"] = tfm;
        }

        // Time info (high precision)
        replacements["time"] = _clock.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss.fffffff", CultureInfo.InvariantCulture);

        return replacements;
    }

    private static Dictionary<string, string> MergeReplacements(Dictionary<string, string> defaultReplacements, IDictionary<string, string>? customReplacements)
    {
        if (customReplacements is null || customReplacements.Count == 0)
        {
            return defaultReplacements;
        }

        var merged = new Dictionary<string, string>(defaultReplacements, StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kvp in customReplacements)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private static string GetOperatingSystemName()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
            : "unknown";
}
