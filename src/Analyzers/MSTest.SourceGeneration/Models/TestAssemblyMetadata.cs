// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

/// <summary>
/// Equatable snapshot of a discovered test class. Source-generator pipeline values must be
/// value-equatable so that incremental caching works; this record carries primitive data only.
/// </summary>
internal sealed record TestClassMetadata(
    string FullyQualifiedName,
    string DisplayName,
    string? Namespace,
    EquatableArray<TestMethodMetadata> Methods,
    EquatableArray<string> BaseTypeFullyQualifiedNames);

/// <summary>
/// Equatable snapshot of a discovered test method.
/// </summary>
internal sealed record TestMethodMetadata(string Name, EquatableArray<string> ParameterTypes);

/// <summary>
/// Equatable snapshot for the full test assembly, used as the input to the emitter.
/// </summary>
internal sealed record TestAssemblyMetadata(
    string AssemblyName,
    EquatableArray<TestClassMetadata> Classes);
