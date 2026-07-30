// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.OpenTelemetry;

/// <summary>
/// Builds the OpenTelemetry <c>Resource</c> attributes that describe *where* a test run happened.
/// </summary>
/// <remarks>
/// Traces and metrics coming out of a test run are only actionable when you can tell one run apart from another:
/// which assembly, which machine, which CI pipeline, which branch and commit. Those belong on the resource rather
/// than on each span, because they are constant for the whole process and backends index them.
/// </remarks>
internal static class TestingPlatformResourceDetector
{
    internal const string UnknownServiceName = "unknown_test_service";

    public static IEnumerable<KeyValuePair<string, object>> GetResourceAttributes()
    {
        foreach (KeyValuePair<string, object> attribute in GetProcessAttributes())
        {
            yield return attribute;
        }

        foreach (KeyValuePair<string, object> attribute in GetCiAttributes())
        {
            yield return attribute;
        }
    }

    public static string GetServiceName()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable(OpenTelemetryEnvironmentVariables.ServiceName);
        return !OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace(fromEnvironment)
            ? fromEnvironment
            : Assembly.GetEntryAssembly()?.GetName().Name ?? UnknownServiceName;
    }

    public static string? GetServiceVersion()
        => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

    private static IEnumerable<KeyValuePair<string, object>> GetProcessAttributes()
    {
        yield return new("host.name", Environment.MachineName);
        yield return new("host.arch", RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant());
        yield return new("os.type", GetOsType());
        yield return new("os.description", RuntimeInformation.OSDescription);
        yield return new("process.pid", GetCurrentProcessId());
        yield return new("process.runtime.name", ".NET");
        yield return new("process.runtime.description", RuntimeInformation.FrameworkDescription);

        if (Assembly.GetEntryAssembly()?.GetName().Name is { } entryAssemblyName)
        {
            yield return new("test.assembly.name", entryAssemblyName);
        }
    }

    private static int GetCurrentProcessId()
    {
#if NET
        return Environment.ProcessId;
#else
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.Id;
#endif
    }

    private static string GetOsType()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : "other";

    /// <summary>
    /// Detects the CI provider and maps its environment variables onto the OpenTelemetry <c>cicd.*</c> and
    /// <c>vcs.*</c> conventions, so a failing test can be correlated with the pipeline run and commit that produced it.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, object>> GetCiAttributes()
    {
        if (IsTrue(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
        {
            yield return new("cicd.provider.name", "github_actions");
            foreach (KeyValuePair<string, object> attribute in Map(
                ("cicd.pipeline.name", "GITHUB_WORKFLOW"),
                ("cicd.pipeline.run.id", "GITHUB_RUN_ID"),
                ("cicd.pipeline.task.name", "GITHUB_JOB"),
                ("vcs.ref.head.name", "GITHUB_REF_NAME"),
                ("vcs.ref.head.revision", "GITHUB_SHA"),
                ("vcs.repository.name", "GITHUB_REPOSITORY")))
            {
                yield return attribute;
            }

            yield break;
        }

        if (IsTrue(Environment.GetEnvironmentVariable("TF_BUILD")))
        {
            yield return new("cicd.provider.name", "azure_pipelines");
            foreach (KeyValuePair<string, object> attribute in Map(
                ("cicd.pipeline.name", "BUILD_DEFINITIONNAME"),
                ("cicd.pipeline.run.id", "BUILD_BUILDID"),
                ("cicd.pipeline.task.run.id", "SYSTEM_JOBID"),
                ("vcs.ref.head.name", "BUILD_SOURCEBRANCHNAME"),
                ("vcs.ref.head.revision", "BUILD_SOURCEVERSION"),
                ("vcs.repository.url.full", "BUILD_REPOSITORY_URI")))
            {
                yield return attribute;
            }

            yield break;
        }

        if (IsTrue(Environment.GetEnvironmentVariable("GITLAB_CI")))
        {
            yield return new("cicd.provider.name", "gitlab");
            foreach (KeyValuePair<string, object> attribute in Map(
                ("cicd.pipeline.name", "CI_PIPELINE_NAME"),
                ("cicd.pipeline.run.id", "CI_PIPELINE_ID"),
                ("cicd.pipeline.task.run.id", "CI_JOB_ID"),
                ("vcs.ref.head.name", "CI_COMMIT_REF_NAME"),
                ("vcs.ref.head.revision", "CI_COMMIT_SHA"),
                ("vcs.repository.url.full", "CI_REPOSITORY_URL")))
            {
                yield return attribute;
            }

            yield break;
        }

        if (!OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JENKINS_URL")))
        {
            yield return new("cicd.provider.name", "jenkins");
            foreach (KeyValuePair<string, object> attribute in Map(
                ("cicd.pipeline.name", "JOB_NAME"),
                ("cicd.pipeline.run.id", "BUILD_NUMBER"),
                ("vcs.ref.head.name", "GIT_BRANCH"),
                ("vcs.ref.head.revision", "GIT_COMMIT"),
                ("vcs.repository.url.full", "GIT_URL")))
            {
                yield return attribute;
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, object>> Map(params (string AttributeName, string EnvironmentVariableName)[] mappings)
    {
        foreach ((string attributeName, string environmentVariableName) in mappings)
        {
            string? value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (!OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace(value))
            {
                yield return new KeyValuePair<string, object>(attributeName, value);
            }
        }
    }

    private static bool IsTrue(string? value)
        => value is "1" or "true" or "True" or "TRUE";
}
