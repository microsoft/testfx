// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal sealed class CiRunSummaryModule
{
    public string AssemblyName { get; set; } = string.Empty;

    public string ModulePath { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = string.Empty;

    public string Architecture { get; set; } = string.Empty;

    public string ExecutionId { get; set; } = string.Empty;

    public string SessionUid { get; set; } = string.Empty;

    public string RequestedOutputPath { get; set; } = string.Empty;

    public int AttemptNumber { get; set; }

    public int ExitCode { get; set; }

    public long TotalTests { get; set; }

    public long PassedTests { get; set; }

    public long FailedTests { get; set; }

    public long SkippedTests { get; set; }

    public long TestDurationTicks { get; set; }

    public CiRunSummaryTest[] Failures { get; set; } = [];

    public CiRunSummaryTest[] SlowestTests { get; set; } = [];

    public CiRunSummaryFailingClass[] TopFailingClasses { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? GitHubActionsStepSummarySections { get; set; }
}

internal sealed class CiRunSummaryTest
{
    public string DisplayName { get; set; } = string.Empty;

    public string FullyQualifiedName { get; set; } = string.Empty;

    public long DurationTicks { get; set; }
}

internal sealed class CiRunSummaryFailingClass
{
    public string ClassName { get; set; } = string.Empty;

    public long FailureCount { get; set; }
}

internal sealed class CiRunSummaryAggregate
{
    public CiRunSummaryAggregate(
        IReadOnlyList<CiRunSummaryModule> modules,
        ArtifactPostProcessingContext context,
        long totalTests,
        long passedTests,
        long failedTests,
        long skippedTests,
        TimeSpan? duration,
        int? exitCode,
        bool hasAuthoritativeRunSummary,
        bool isPartial)
    {
        Modules = modules;
        Context = context;
        TotalTests = totalTests;
        PassedTests = passedTests;
        FailedTests = failedTests;
        SkippedTests = skippedTests;
        Duration = duration;
        ExitCode = exitCode;
        HasAuthoritativeRunSummary = hasAuthoritativeRunSummary;
        IsPartial = isPartial;
    }

    public IReadOnlyList<CiRunSummaryModule> Modules { get; }

    public ArtifactPostProcessingContext Context { get; }

    public long TotalTests { get; }

    public long PassedTests { get; }

    public long FailedTests { get; }

    public long SkippedTests { get; }

    public TimeSpan? Duration { get; }

    public int? ExitCode { get; }

    public bool HasAuthoritativeRunSummary { get; }

    public bool IsPartial { get; }
}

internal static partial class CiRunSummaryAggregation
{
    private const int SchemaVersion = 1;
    private const int MaxFailures = 20;
    private const int MaxSlowestTests = 10;
    private const int MaxTopFailingClasses = 5;
    private const string FragmentDirectoryName = ".ci-summary-fragments";
    private const string MergedDirectoryName = "merged";

    public static CiRunSummaryModule CreateModule(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string modulePath,
        string targetFramework,
        string architecture,
        string? executionId,
        string sessionUid,
        int attemptNumber,
        int exitCode,
        string? requestedOutputPath = null)
    {
        long passed = 0;
        long failed = 0;
        long skipped = 0;
        long durationTicks = 0;
        var failures = new List<TestRecord>();
        var failingByClass = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (TestRecord record in records)
        {
            durationTicks = checked(durationTicks + record.Duration.Ticks);
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    failures.Add(record);
                    string className = GetClassName(record.FullyQualifiedName);
                    failingByClass[className] = failingByClass.TryGetValue(className, out long count) ? count + 1 : 1;
                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        return new CiRunSummaryModule
        {
            AssemblyName = assemblyName,
            ModulePath = modulePath,
            TargetFramework = targetFramework,
            Architecture = architecture,
            ExecutionId = executionId ?? string.Empty,
            SessionUid = sessionUid,
            RequestedOutputPath = requestedOutputPath ?? string.Empty,
            AttemptNumber = attemptNumber,
            ExitCode = exitCode,
            TotalTests = checked(passed + failed + skipped),
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            TestDurationTicks = durationTicks,
            Failures =
            [
                .. failures
                    .OrderBy(record => record.FullyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(record => record.DisplayName, StringComparer.Ordinal)
                    .Take(MaxFailures)
                    .Select(ToSummaryTest),
            ],
            SlowestTests =
            [
                .. records
                    .Where(record => record.Duration > TimeSpan.Zero)
                    .OrderByDescending(record => record.Duration)
                    .ThenBy(record => record.FullyQualifiedName, StringComparer.Ordinal)
                    .Take(MaxSlowestTests)
                    .Select(ToSummaryTest),
            ],
            TopFailingClasses =
            [
                .. failingByClass
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(MaxTopFailingClasses)
                    .Select(pair => new CiRunSummaryFailingClass { ClassName = pair.Key, FailureCount = pair.Value }),
            ],
        };
    }

    public static async Task<string> WriteFragmentAsync(
        string resultsDirectory,
        string provider,
        string providerSlug,
        CiRunSummaryModule module)
    {
        string fragmentDirectory = Path.Combine(resultsDirectory, FragmentDirectoryName);
        Directory.CreateDirectory(fragmentDirectory);
        string identity = $"{provider}\0{module.ModulePath}\0{module.TargetFramework}\0{module.Architecture}\0{module.ExecutionId}\0{module.SessionUid}";
        string fileName = $"{providerSlug}-{HashIdentity(identity).Substring(0, 32)}.json";
        string path = Path.Combine(fragmentDirectory, fileName);
        var fragment = new CiRunSummaryFragment
        {
            SchemaVersion = SchemaVersion,
            Provider = provider,
            Module = module,
        };
        string json = JsonSerializer.Serialize(fragment, CiRunSummaryJsonContext.Default.CiRunSummaryFragment);

        await WriteAtomicAsync(path, json).ConfigureAwait(false);
        return path;
    }

    public static CiRunSummaryAggregate ReadAndAggregate(
        IReadOnlyList<InputArtifact> inputs,
        string provider,
        ArtifactPostProcessingContext context)
    {
        var modules = new List<CiRunSummaryModule>(inputs.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (InputArtifact input in inputs)
        {
            CiRunSummaryFragment? fragment;
            using (FileStream stream = File.OpenRead(input.Path))
            {
                fragment = JsonSerializer.Deserialize(stream, CiRunSummaryJsonContext.Default.CiRunSummaryFragment);
            }

            if (fragment is null
                || fragment.SchemaVersion != SchemaVersion
                || !string.Equals(fragment.Provider, provider, StringComparison.Ordinal)
                || fragment.Module is null)
            {
                throw new FormatException($"Invalid {provider} summary fragment '{input.Path}'.");
            }

            ValidateModule(fragment.Module, input);
            string identity = GetModuleIdentity(fragment.Module);
            if (!identities.Add(identity))
            {
                throw new FormatException($"Duplicate {provider} summary fragment identity '{identity}'.");
            }

            modules.Add(fragment.Module);
        }

        modules.Sort(CompareModules);

        long observedPassed = 0;
        long observedFailed = 0;
        long observedSkipped = 0;
        foreach (CiRunSummaryModule module in modules)
        {
            observedPassed = checked(observedPassed + module.PassedTests);
            observedFailed = checked(observedFailed + module.FailedTests);
            observedSkipped = checked(observedSkipped + module.SkippedTests);
        }

        long observedTotal = checked(observedPassed + observedFailed + observedSkipped);
        ArtifactPostProcessingRunSummary? runSummary = context.RunSummary;
        return runSummary is null
            ? new CiRunSummaryAggregate(
                modules,
                context,
                observedTotal,
                observedPassed,
                observedFailed,
                observedSkipped,
                duration: null,
                exitCode: null,
                hasAuthoritativeRunSummary: false,
                isPartial: context.IsTruncated)
            : new CiRunSummaryAggregate(
                modules,
                context,
                runSummary.TotalTests,
                runSummary.PassedTests,
                runSummary.FailedTests,
                runSummary.SkippedTests,
                runSummary.Duration,
                runSummary.ExitCode,
                hasAuthoritativeRunSummary: true,
                isPartial: context.IsTruncated);
    }

    public static string CreateAggregationId(IReadOnlyList<InputArtifact> inputs)
    {
        string identity = string.Join(
            "\0",
            inputs.Select(input => $"{Path.GetFullPath(input.Path)}\0{input.ExecutionId}")
                .OrderBy(value => value, StringComparer.Ordinal));
        return HashIdentity(identity).Substring(0, 32);
    }

    private static string HashIdentity(string identity)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(identity);
#if NETCOREAPP
        byte[] hash = SHA256.HashData(bytes);
#else
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(bytes);
#endif
        var builder = new StringBuilder(capacity: hash.Length * 2);
        for (int i = 0; i < hash.Length; i++)
        {
            builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static string GetMergedOutputPath(string outputDirectory, string providerSlug, string aggregationId)
    {
        string mergedDirectory = Path.Combine(outputDirectory, MergedDirectoryName);
        Directory.CreateDirectory(mergedDirectory);
        return (File.GetAttributes(mergedDirectory) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint
            ? throw new IOException($"The merged summary directory '{mergedDirectory}' cannot be a reparse point.")
            : Path.Combine(mergedDirectory, $"{providerSlug}-summary-{aggregationId}.md");
    }

    public static Task WriteOutputAsync(string path, string content)
        => WriteAtomicAsync(path, content);

    private static void ValidateModule(CiRunSummaryModule module, InputArtifact input)
    {
        if (RoslynString.IsNullOrWhiteSpace(module.AssemblyName)
            || RoslynString.IsNullOrWhiteSpace(module.ModulePath)
            || RoslynString.IsNullOrWhiteSpace(module.TargetFramework)
            || RoslynString.IsNullOrWhiteSpace(module.Architecture)
            || RoslynString.IsNullOrWhiteSpace(module.SessionUid)
            || module.AttemptNumber <= 0
            || module.TotalTests < 0
            || module.PassedTests < 0
            || module.FailedTests < 0
            || module.SkippedTests < 0
            || module.TestDurationTicks < 0
            || checked(module.PassedTests + module.FailedTests + module.SkippedTests) != module.TotalTests
            || module.Failures is null
            || module.SlowestTests is null
            || module.TopFailingClasses is null
            || module.Failures.Any(test => !IsValidTest(test))
            || module.SlowestTests.Any(test => !IsValidTest(test))
            || module.TopFailingClasses.Any(item => RoslynString.IsNullOrWhiteSpace(item.ClassName) || item.FailureCount <= 0))
        {
            throw new FormatException($"Invalid CI summary module in '{input.Path}'.");
        }

        if (input.ProducingTestModule is not null
            && !PathsEqual(input.ProducingTestModule, module.ModulePath))
        {
            throw new FormatException($"CI summary module provenance does not match '{input.Path}'.");
        }

        if (input.TargetFramework is not null
            && !string.Equals(input.TargetFramework, module.TargetFramework, StringComparison.Ordinal))
        {
            throw new FormatException($"CI summary target framework provenance does not match '{input.Path}'.");
        }

        if (input.Architecture is not null
            && !string.Equals(input.Architecture, module.Architecture, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"CI summary architecture provenance does not match '{input.Path}'.");
        }

        if (input.ExecutionId is not null
            && !string.Equals(input.ExecutionId, module.ExecutionId, StringComparison.Ordinal))
        {
            throw new FormatException($"CI summary execution provenance does not match '{input.Path}'.");
        }
    }

    private static bool IsValidTest(CiRunSummaryTest test)
        => !RoslynString.IsNullOrWhiteSpace(test.DisplayName)
            && !RoslynString.IsNullOrWhiteSpace(test.FullyQualifiedName)
            && test.DurationTicks >= 0;

    private static int CompareModules(CiRunSummaryModule left, CiRunSummaryModule right)
    {
        int result = StringComparer.Ordinal.Compare(left.AssemblyName, right.AssemblyName);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.TargetFramework, right.TargetFramework);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.Architecture, right.Architecture);
        result = result != 0 ? result : left.AttemptNumber.CompareTo(right.AttemptNumber);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.ModulePath, right.ModulePath);
        result = result != 0 ? result : StringComparer.Ordinal.Compare(left.ExecutionId, right.ExecutionId);
        return result != 0 ? result : StringComparer.Ordinal.Compare(left.SessionUid, right.SessionUid);
    }

    private static string GetModuleIdentity(CiRunSummaryModule module)
        => $"{module.ModulePath}\0{module.TargetFramework}\0{module.Architecture}\0{module.ExecutionId}\0{module.AttemptNumber}\0{module.SessionUid}";

    private static CiRunSummaryTest ToSummaryTest(TestRecord record)
        => new()
        {
            DisplayName = record.DisplayName,
            FullyQualifiedName = record.FullyQualifiedName,
            DurationTicks = record.Duration.Ticks,
        };

    private static string GetClassName(string fullyQualifiedName)
    {
        if (RoslynString.IsNullOrEmpty(fullyQualifiedName))
        {
            return "(unknown)";
        }

        int lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot <= 0 ? "(unknown)" : fullyQualifiedName.Substring(0, lastDot);
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static async Task WriteTextAsync(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content).ConfigureAwait(false);
    }

    private static async Task WriteAtomicAsync(string path, string content)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!RoslynString.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await WriteTextAsync(tempPath, content).ConfigureAwait(false);
#if NETCOREAPP
            File.Move(tempPath, fullPath, overwrite: true);
#else
            File.Delete(fullPath);
            File.Move(tempPath, fullPath);
#endif
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup must not hide a successful write or its primary failure.
            }
        }
    }

    private sealed class CiRunSummaryFragment
    {
        public int SchemaVersion { get; set; }

        public string Provider { get; set; } = string.Empty;

        public CiRunSummaryModule? Module { get; set; }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
    [JsonSerializable(typeof(CiRunSummaryFragment))]
    private sealed partial class CiRunSummaryJsonContext : JsonSerializerContext;
}

#pragma warning restore RS0051
