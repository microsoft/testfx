// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public sealed class TestHost
{
    private readonly string _testHostModuleName;

    [SuppressMessage("Style", "IDE0032:Use auto property", Justification = "It's causing runtime bug")]
    private static int s_maxOutstandingExecutions = Environment.ProcessorCount;
    private static SemaphoreSlim s_maxOutstandingExecutions_semaphore = new(s_maxOutstandingExecutions, s_maxOutstandingExecutions);

    private TestHost(string testHostFullName, string testHostModuleName)
    {
        FullName = testHostFullName;
        DirectoryName = Path.GetDirectoryName(testHostFullName)!;
        _testHostModuleName = testHostModuleName;
    }

    public static int MaxOutstandingExecutions
    {
        get => s_maxOutstandingExecutions;

        set
        {
            s_maxOutstandingExecutions = value;
            s_maxOutstandingExecutions_semaphore.Dispose();
            s_maxOutstandingExecutions_semaphore = new SemaphoreSlim(s_maxOutstandingExecutions, s_maxOutstandingExecutions);
        }
    }

    public string FullName { get; }

    public string DirectoryName { get; }

    public async Task<TestHostResult> ExecuteAsync(
        string? command = null,
        Dictionary<string, string?>? environmentVariables = null,
        bool disableTelemetry = true,
        bool disableAzureDevOpsOutput = true,
        CancellationToken cancellationToken = default)
    {
        await s_maxOutstandingExecutions_semaphore.WaitAsync(cancellationToken);
        try
        {
            if (command?.StartsWith(_testHostModuleName, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                throw new InvalidOperationException($"Command should not start with module name '{_testHostModuleName}'.");
            }

            environmentVariables ??= [];

            if (disableTelemetry)
            {
                environmentVariables.Add("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
            }

            if (disableAzureDevOpsOutput)
            {
                // Acceptance tests assert against literal stdout content and many of them run on Azure DevOps
                // (where TF_BUILD=true is set). Without this opt-out the child test host would emit
                // ##vso[task.logissue type=...] logging commands for every warning/error/exception, which
                // breaks index-based line assertions.
                environmentVariables.TryAdd("TESTINGPLATFORM_AZDO_OUTPUT", "off");
            }

            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                // Skip all unwanted environment variables.
                string? key = entry.Key.ToString();
                if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                // We use TryAdd to let tests "overwrite" existing environment variables.
                // Consider that the given dictionary has "TESTINGPLATFORM_UI_LANGUAGE" as a key.
                // And also Environment.GetEnvironmentVariables() is returning TESTINGPLATFORM_UI_LANGUAGE.
                // In that case, we do a "TryAdd" which effectively means the value from the original dictionary wins.
                environmentVariables.TryAdd(key!, entry!.Value!.ToString()!);
            }

            // Define DOTNET_ROOT to point to the dotnet we install for this repository, to avoid
            // computer configuration having impact on our tests.
            environmentVariables.Add("DOTNET_ROOT", $"{RootFinder.Find()}/.dotnet");

            string finalArguments = command ?? string.Empty;

            CommandLine commandLine = new();
            // Disable ANSI rendering so tests have easier time parsing the output.
            // Disable progress so tests don't mix progress with overall progress, and with test process output.
            int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
                $"{FullName} --no-ansi --progress off {finalArguments}",
                environmentVariables: environmentVariables,
                workingDirectory: null,
                cleanDefaultEnvironmentVariableIfCustomAreProvided: true,
                cancellationToken: cancellationToken);
            string fullCommand = command is not null ? $"{FullName} {command}" : FullName;
            return new TestHostResult(fullCommand, exitCode, commandLine.StandardOutput, commandLine.StandardOutputLines, commandLine.ErrorOutput, commandLine.ErrorOutputLines);
        }
        finally
        {
            s_maxOutstandingExecutions_semaphore.Release();
        }
    }

    public static TestHost LocateFrom(string rootFolder, string testHostModuleNameWithoutExtension)
    {
        string moduleName = $"{testHostModuleNameWithoutExtension}{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty)}";

        // Try to find module in current dir, useful when we use to close
        return File.Exists(Path.Combine(rootFolder, moduleName))
            ? new TestHost(Path.Combine(rootFolder, moduleName), testHostModuleNameWithoutExtension)
            : throw new FileNotFoundException("Test host file not found", Path.Combine(rootFolder, moduleName));
    }

    public static TestHost LocateFrom(
        string rootFolder,
        string testHostModuleNameWithoutExtension,
        string tfm,
        string rid = "",
        Verb verb = Verb.build,
        BuildConfiguration buildConfiguration = BuildConfiguration.Release,
        MetadataMode metadataMode = MetadataMode.Reflection)
    {
        string moduleName = $"{testHostModuleNameWithoutExtension}{(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty)}";

        // Each source-generation build variant is redirected to its own bin sub-folder (see
        // AcceptanceSourceGen), so it never overwrites the default reflection build under bin.
        string expectedRootPath = metadataMode == MetadataMode.Reflection
            ? Path.Combine(rootFolder, "bin", buildConfiguration.ToString(), tfm)
            : Path.Combine(rootFolder, "bin", AcceptanceSourceGen.GetOutputSubFolder(metadataMode), buildConfiguration.ToString(), tfm);

        // Directory.GetFiles throws a non-actionable DirectoryNotFoundException when the expected
        // output folder is missing (typically because the fixture did not build this metadata variant,
        // for example a fixture that did not opt the mode into SourceGenMetadataModes). Surface a clear
        // message instead.
        if (!Directory.Exists(expectedRootPath))
        {
            throw new InvalidOperationException(
                $"Expected build output folder for metadata mode '{metadataMode}' was not found: '{expectedRootPath}'. " +
                $"Ensure the asset fixture builds the '{metadataMode}' variant (see TestAssetFixtureBase.SourceGenMetadataModes).");
        }

        string[] executables = Directory.GetFiles(expectedRootPath, moduleName, SearchOption.AllDirectories);
        string? expectedPath = executables.SingleOrDefault(p => p.Contains(rid) && p.Contains(verb == Verb.publish ? "publish" : string.Empty));

        return expectedPath is null
            ? throw new InvalidOperationException($"Host '{moduleName}' not found in '{expectedRootPath}'")
            : new TestHost(expectedPath, testHostModuleNameWithoutExtension);
    }
}
