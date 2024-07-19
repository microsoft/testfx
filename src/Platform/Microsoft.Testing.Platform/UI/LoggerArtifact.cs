// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if !NET7_0_OR_GREATER
#endif
#if !NET7_0_OR_GREATER
#endif
namespace Microsoft.Testing.Platform.UI;

internal record class LoggerArtifact(bool OutOfProcess, string? Assembly, string? TargetFramework, string? Architecture, string? TestName, string Path);
