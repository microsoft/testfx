// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;
using Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;

internal static class GeneratorCompilationResultHelpers
{
    public static GeneratorCompilationResult AssertSuccessfulGeneration(this GeneratorCompilationResult result)
    {
        result.EmitResult.Success.Should().BeTrue("compilation should have been successful.\n"
            + $"Diagnostics: {result.EmitResult.Diagnostics.Length}\n"
            + $"Code:\n{result.FailingGeneratedCode}");
        result.TimingInfo.ElapsedTime.Should().BeGreaterThan(TimeSpan.Zero);
        result.EmitResult.Diagnostics.Where(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).Should().BeEmpty();
        result.RunResult.Diagnostics.Should().BeEmpty();
        return result;
    }

    public static GeneratorCompilationResult AssertFailedGeneration(this GeneratorCompilationResult result, params string[] diagnostics)
    {
        result.EmitResult.Success.Should().BeFalse();
        result.TimingInfo.ElapsedTime.Should().BeGreaterThan(TimeSpan.Zero);
        result.RunResult.Diagnostics.Should().BeEmpty();
        result.EmitResult.Diagnostics.Should().HaveSameCount(diagnostics);

        for (int i = 0; i < diagnostics.Length; i++)
        {
            result.EmitResult.Diagnostics.Select(d => d.ToString()).Should().ContainMatch(diagnostics[i]);
        }

        return result;
    }
}
