// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// An artifact / attachment that was reported during run.
/// </summary>
[Embedded]
internal sealed record TestRunArtifact(bool OutOfProcess, string? Assembly, string? TargetFramework, string? Architecture, string? ExecutionId, string? TestName, string Path);
