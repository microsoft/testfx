// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal static class AzureDevOpsConstants
{
    /// <summary>
    /// Name of the Azure Pipelines environment variable that indicates whether the
    /// current process is running inside an Azure DevOps build/release agent.
    /// </summary>
    public const string TfBuildEnvironmentVariableName = "TF_BUILD";

    /// <summary>
    /// Expected value of <see cref="TfBuildEnvironmentVariableName"/> when running
    /// inside an Azure DevOps build/release agent.
    /// </summary>
    public const string TfBuildEnabledValue = "true";

    /// <summary>
    /// Returns <see langword="true"/> when the current process is running inside an
    /// Azure DevOps build/release agent (i.e. <c>TF_BUILD=true</c>).
    /// </summary>
    public static bool IsRunningInAzureDevOps(IEnvironment environment)
        => string.Equals(
            environment.GetEnvironmentVariable(TfBuildEnvironmentVariableName),
            TfBuildEnabledValue,
            StringComparison.OrdinalIgnoreCase);
}
