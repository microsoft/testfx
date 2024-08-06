﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Console;

/// <summary>
/// An artifact / attachment that was reported during run.
/// </summary>
internal record class TestRunArtifact(bool OutOfProcess, string? Assembly, string? TargetFramework, string? Architecture, string? TestName, string Path);
