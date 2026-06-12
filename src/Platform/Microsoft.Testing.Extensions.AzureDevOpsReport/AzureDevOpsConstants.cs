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
    /// inside an Azure DevOps build/release agent. Azure DevOps actually sets the
    /// variable to <c>True</c> (capitalized), but the comparison performed by
    /// <see cref="IsRunningInAzureDevOps(IEnvironment)"/> is case-insensitive so any
    /// casing of <c>true</c> is accepted.
    /// </summary>
    public const string TfBuildEnabledValue = "true";

    /// <summary>
    /// Returns <see langword="true"/> when the current process is running inside an
    /// Azure DevOps build/release agent (i.e. when the <c>TF_BUILD</c> environment
    /// variable is set to <c>true</c>). The comparison is case-insensitive so the
    /// value set by Azure DevOps (<c>True</c>) and any other casing variant
    /// (<c>true</c>, <c>TRUE</c>, ...) are all treated as enabled.
    /// </summary>
    public static bool IsRunningInAzureDevOps(IEnvironment environment)
        => string.Equals(
            environment.GetEnvironmentVariable(TfBuildEnvironmentVariableName),
            TfBuildEnabledValue,
            StringComparison.OrdinalIgnoreCase);
}
