// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;

namespace MSTest.Analyzers.Test;

internal static class VisualBasicVerifierHelper
{
    /// <summary>
    /// Gets by default, the compiler reports diagnostics for nullable reference types at
    /// <see cref="DiagnosticSeverity.Warning"/>, and the analyzer test framework defaults to only validating
    /// diagnostics at <see cref="DiagnosticSeverity.Error"/>. This map contains all compiler diagnostic IDs
    /// related to nullability mapped to <see cref="ReportDiagnostic.Error"/>, which is then used to enable all
    /// of these warnings for default validation during analyzer and code fix tests.
    /// </summary>
    internal static ImmutableDictionary<string, ReportDiagnostic> NullableWarnings { get; } = GetNullableWarningsFromCompiler();

    private static ImmutableDictionary<string, ReportDiagnostic> GetNullableWarningsFromCompiler()
    {
        string[] args = ["/warnaserror:nullable"];
        VisualBasicCommandLineArguments commandLineArguments = VisualBasicCommandLineParser.Default.Parse(args, baseDirectory: Environment.CurrentDirectory, sdkDirectory: Environment.CurrentDirectory);
        ImmutableDictionary<string, ReportDiagnostic> nullableWarnings = commandLineArguments.CompilationOptions.SpecificDiagnosticOptions;

        // Workaround for https://github.com/dotnet/roslyn/issues/41610 
        // Note: VB equivalent diagnostic IDs may be different from C#
        nullableWarnings = nullableWarnings
            .SetItem("BC42104", ReportDiagnostic.Error) // VB equivalent for nullable warnings
            .SetItem("BC42105", ReportDiagnostic.Error);

        return nullableWarnings;
    }
}