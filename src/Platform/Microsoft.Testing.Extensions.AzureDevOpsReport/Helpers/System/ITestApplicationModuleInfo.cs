// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport.Helpers;

/// <summary>
/// Minimal local replacement for Microsoft.Testing.Platform.Services.ITestApplicationModuleInfo,
/// exposing only the single member required by Microsoft.Testing.Extensions.AzureDevOpsReport.
/// Avoids cross-assembly access to Platform internals so the extension can drop the IVT.
/// </summary>
internal interface ITestApplicationModuleInfo
{
    string? TryGetAssemblyName();
}
