// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

internal sealed class GeneratorCompilationResult(GeneratorDriverRunResult runResult, GeneratorDriverTimingInfo timingInfo,
    EmitResult emitResult, string? failingGeneratedCode)
{
    public ImmutableArray<SyntaxTree> GeneratedTrees => RunResult.GeneratedTrees;

    public GeneratorDriverRunResult RunResult { get; } = runResult;

    public GeneratorDriverTimingInfo TimingInfo { get; } = timingInfo;

    public EmitResult EmitResult { get; } = emitResult;

    public string? FailingGeneratedCode { get; } = failingGeneratedCode;
}
