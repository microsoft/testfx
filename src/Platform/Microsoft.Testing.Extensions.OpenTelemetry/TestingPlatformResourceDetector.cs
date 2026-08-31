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
        yield return new("host.arch", GetHostArchitecture());

        // os.type has no defined catch-all value upstream, so an unrecognised platform omits the attribute rather
        // than inventing one.
        if (GetOsType() is { } osType)
        {
            yield return new("os.type", osType);
        }

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

    private static string? GetOsType()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
            : null;

    /// <summary>
    /// Maps <see cref="Architecture"/> onto the values allowed by the OpenTelemetry <c>host.arch</c> enum, which
    /// does not use the .NET spellings (for example it says <c>amd64</c>, not <c>x64</c>).
    /// </summary>
    private static string GetHostArchitecture()
        => RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm32",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
        };

    /// <summary>
    /// Detects the CI provider and maps its environment variables onto the OpenTelemetry <c>cicd.*</c> and
    /// <c>vcs.*</c> conventions, so a failing test can be correlated with the pipeline run and commit that produced it.
    /// </summary>
    /// <remarks>
    /// <c>cicd.pipeline.*</c> and <c>vcs.*</c> are upstream-defined (release candidate). <c>cicd.provider.name</c>
    /// is <b>not</b>: as of semantic conventions 1.43.0 there is no attribute identifying the CI system, so this is
    /// a platform extension sitting in the namespace where an upstream definition would land.
    /// </remarks>
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
                if (attributeName == "vcs.repository.url.full")
                {
                    value = RemoveUrlUserInfo(value);
                }

                yield return new KeyValuePair<string, object>(attributeName, value);
            }
        }
    }

    private static string RemoveUrlUserInfo(string value)
    {
        int schemeSeparatorIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex < 0)
        {
            return value;
        }

        int authorityStartIndex = schemeSeparatorIndex + 3;
        int authorityEndIndex = value.Length;
        int pathIndex = value.IndexOf('/', authorityStartIndex);
        if (pathIndex >= 0)
        {
            authorityEndIndex = pathIndex;
        }

        int queryIndex = value.IndexOf('?', authorityStartIndex);
        if (queryIndex >= 0 && queryIndex < authorityEndIndex)
        {
            authorityEndIndex = queryIndex;
        }

        int fragmentIndex = value.IndexOf('#', authorityStartIndex);
        if (fragmentIndex >= 0 && fragmentIndex < authorityEndIndex)
        {
            authorityEndIndex = fragmentIndex;
        }

        int authorityLength = authorityEndIndex - authorityStartIndex;
        if (authorityLength <= 0)
        {
            return value;
        }

        int userInfoEndIndex = value.LastIndexOf('@', authorityEndIndex - 1, authorityLength);
        return userInfoEndIndex < authorityStartIndex
            ? value
            : value.Substring(0, authorityStartIndex) + value.Substring(userInfoEndIndex + 1);
    }

    private static bool IsTrue(string? value)
        => value is "1" or "true" or "True" or "TRUE";
}
