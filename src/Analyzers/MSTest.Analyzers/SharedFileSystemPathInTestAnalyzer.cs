// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0077: <inheritdoc cref="Resources.SharedFileSystemPathInTestTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class SharedFileSystemPathInTestAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.SharedFileSystemPathInTestTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.SharedFileSystemPathInTestMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.SharedFileSystemPathInTestDescription), Resources.ResourceManager, typeof(Resources));

    /// <summary>
    /// Gets the diagnostic descriptor reported by this analyzer.
    /// </summary>
    public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.SharedFileSystemPathInTestRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? fileSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOFile);
            INamedTypeSymbol? directorySymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIODirectory);
            if (fileSymbol is null && directorySymbol is null)
            {
                return;
            }

            INamedTypeSymbol? parallelizeAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingParallelizeAttribute);
            INamedTypeSymbol? doNotParallelizeAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDoNotParallelizeAttribute);
            if (!ParallelSafetyHelper.IsParallelizationInEffect(compilation, context.Options, parallelizeAttributeSymbol, doNotParallelizeAttributeSymbol))
            {
                return;
            }

            INamedTypeSymbol? testMethodAttributeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
            ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols = ParallelSafetyHelper.GetFixtureAttributeSymbols(compilation);
            if (testMethodAttributeSymbol is null && fixtureAttributeSymbols.IsEmpty)
            {
                return;
            }

            context.RegisterOperationAction(
                context => AnalyzeInvocation(context, fileSymbol, directorySymbol, testMethodAttributeSymbol, fixtureAttributeSymbols, doNotParallelizeAttributeSymbol),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? fileSymbol,
        INamedTypeSymbol? directorySymbol,
        INamedTypeSymbol? testMethodAttributeSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        INamedTypeSymbol? doNotParallelizeAttributeSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;
        if (!targetMethod.IsStatic)
        {
            return;
        }

        INamedTypeSymbol containingType = targetMethod.ContainingType;
        bool isFile = SymbolEqualityComparer.Default.Equals(containingType, fileSymbol);
        bool isDirectory = SymbolEqualityComparer.Default.Equals(containingType, directorySymbol);
        if (!isFile && !isDirectory)
        {
            return;
        }

        // Precision: fire ONLY on APIs that create, write, move, or delete a filesystem entry. Reads
        // (File.ReadAllText, File.Exists, Directory.EnumerateFiles, ...) are intentionally excluded - reading a
        // shared fixture at a fixed path is common and safe, and flagging it would be a false positive. Dogfooding
        // proved that flagging path *construction* (e.g. Path.Combine(Path.GetTempPath(), "a.trx")) is a noise
        // generator, because the constructed string is overwhelmingly used as a hash input, a "does-not-exist"
        // sentinel, or mock data rather than for colliding I/O. The analyzer can only see the call, not the
        // resource, so it stays within the statically certain subset: a constant path passed directly to a
        // mutating File.*/Directory.* API. Tracing paths through helpers is left to the parallel-safety-audit skill.
        if (!IsMutatingFileSystemMethod(isFile, targetMethod.Name))
        {
            return;
        }

        if (!TryGetConstantMutatedPathArgument(invocation, isFile, targetMethod.Name, out string? offendingPath) || offendingPath is null)
        {
            return;
        }

        IMethodSymbol? testMethod = ParallelSafetyHelper.GetEnclosingTestMethod(context.ContainingSymbol, fixtureAttributeSymbols, testMethodAttributeSymbol);
        if (testMethod is null)
        {
            return;
        }

        // Opting out of parallelization (sequential phase) removes the collision risk entirely, so stay silent -
        // this matches R1/R2/R3 and keeps specimens like the [DoNotParallelize] environment-mutating tests quiet.
        if (ParallelSafetyHelper.IsOptedOutOfParallelization(testMethod, doNotParallelizeAttributeSymbol, testMethodAttributeSymbol))
        {
            return;
        }

        context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, offendingPath));
    }

    /// <summary>
    /// Recognizes the <c>File.*</c>/<c>Directory.*</c> methods that create, write to, move, replace, delete, or
    /// change the metadata of a filesystem entry. Read-only members (and pure path helpers) are excluded so that
    /// reading a shared fixture at a fixed path never triggers a diagnostic.
    /// </summary>
    private static bool IsMutatingFileSystemMethod(bool isFile, string methodName)
        => isFile
            ? methodName is "WriteAllText" or "WriteAllTextAsync"
                or "WriteAllBytes" or "WriteAllBytesAsync"
                or "WriteAllLines" or "WriteAllLinesAsync"
                or "AppendAllText" or "AppendAllTextAsync"
                or "AppendAllLines" or "AppendAllLinesAsync"
                or "AppendText"
                or "Create" or "CreateText" or "CreateSymbolicLink"
                or "Copy" or "Move" or "Replace" or "Delete"
                or "OpenWrite"
                // 'File.Open' is intentionally excluded: its mode/access arguments can request a read-only handle
                // ('File.Open(path, FileMode.Open, FileAccess.Read)'), so treating every 'Open' as a mutation would be
                // a false positive. Only the unambiguously-writing 'OpenWrite' is flagged, keeping R4 within the
                // statically certain, precision-first subset.
                or "Encrypt" or "Decrypt"
                or "SetAttributes" or "SetUnixFileMode"
                or "SetCreationTime" or "SetCreationTimeUtc"
                or "SetLastAccessTime" or "SetLastAccessTimeUtc"
                or "SetLastWriteTime" or "SetLastWriteTimeUtc"
            : methodName is "CreateDirectory" or "CreateSymbolicLink"
                or "Delete" or "Move"
                or "SetCreationTime" or "SetCreationTimeUtc"
                or "SetLastAccessTime" or "SetLastAccessTimeUtc"
                or "SetLastWriteTime" or "SetLastWriteTimeUtc";

    /// <summary>
    /// Returns the constant string bound to a path parameter that the specific <c>File.*</c>/<c>Directory.*</c>
    /// method actually <em>mutates</em>, when that argument is a compile-time constant (literal or <c>const</c>).
    /// Path roles are per-API: <c>File.Copy</c>/<c>File.Replace</c> <em>read</em> their source, so a constant source
    /// there is not flagged (reading a shared fixture at a fixed path is safe), whereas <c>Move</c> removes its source
    /// and <c>Replace</c> creates a backup, so those positions are mutations. Variable paths return
    /// <see langword="false"/>: the analyzer cannot know whether two tests collide, so it stays silent on them.
    /// </summary>
    private static bool TryGetConstantMutatedPathArgument(IInvocationOperation invocation, bool isFile, string methodName, out string? path)
    {
        path = null;
        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (argument.Parameter is not { } parameter
                || parameter.Type.SpecialType != SpecialType.System_String)
            {
                continue;
            }

            if (!IsMutatedPathParameter(isFile, methodName, parameter.Name))
            {
                continue;
            }

            if (argument.Value.ConstantValue is { HasValue: true, Value: string value }
                && value.Length > 0)
            {
                path = value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Decides whether a given path parameter of a mutating <c>File.*</c>/<c>Directory.*</c> method names a
    /// filesystem entry the call creates, overwrites, moves, or deletes (as opposed to one it only reads).
    /// </summary>
    private static bool IsMutatedPathParameter(bool isFile, string methodName, string parameterName)
        => isFile
            ? methodName switch
            {
                // Copy writes only the destination; the source is read - a constant source must not be flagged.
                "Copy" => parameterName is "destFileName",

                // Move deletes the source and creates the destination, so both positions are mutations.
                "Move" => parameterName is "sourceFileName" or "destFileName",

                // Replace overwrites destinationFileName and creates destinationBackupFileName; sourceFileName is read.
                "Replace" => parameterName is "destinationFileName" or "destinationBackupFileName",

                // Every other mutating File.* member acts on its single `path` argument.
                _ => parameterName is "path",
            }
            : methodName switch
            {
                // Directory.Move deletes the source directory and creates the destination.
                "Move" => parameterName is "sourceDirName" or "destDirName",
                _ => parameterName is "path",
            };
}
