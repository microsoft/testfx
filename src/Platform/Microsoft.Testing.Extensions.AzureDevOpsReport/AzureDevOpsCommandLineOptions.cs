// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Reporting;

internal static class AzureDevOpsCommandLineOptions
{
    public const string AzureDevOpsOptionName = "report-azdo";
    public const string AzureDevOpsReportSeverity = "report-azdo-severity";
    public const string AzureDevOpsUploadArtifactExclude = "report-azdo-upload-artifact-exclude";
    public const string AzureDevOpsUploadArtifactInclude = "report-azdo-upload-artifact-include";
    public const string AzureDevOpsUploadArtifactName = "report-azdo-upload-artifact-name";
    public const string AzureDevOpsUploadArtifacts = "report-azdo-upload-artifacts";
    public const string AzureDevOpsUploadArtifactsModeAll = "all";
    public const string AzureDevOpsUploadArtifactsModeFiles = "files";
    public const string AzureDevOpsUploadArtifactsModeOff = "off";
    public const string AzureDevOpsUploadArtifactsModeTagsOnly = "tags-only";
}
