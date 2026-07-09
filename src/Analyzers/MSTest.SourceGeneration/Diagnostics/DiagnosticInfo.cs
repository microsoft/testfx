// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;

/// <summary>
/// Equatable payload that travels through the incremental-generator pipeline and is
/// reified into a real <see cref="Diagnostic" /> only at the <c>RegisterSourceOutput</c>
/// stage. Holding only the descriptor id (rather than the descriptor itself) keeps the
/// record cheaply equatable across runs.
/// </summary>
internal sealed record DiagnosticInfo(
    string DescriptorId,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic ToDiagnostic()
    {
        DiagnosticDescriptor descriptor = DiagnosticDescriptors.GetById(DescriptorId);
        ImmutableArray<string> args = MessageArgs.AsImmutableArray();
        object?[] formattedArgs = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            formattedArgs[i] = args[i];
        }

        return Diagnostic.Create(descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, formattedArgs);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] messageArgs)
        => new(descriptor.Id, location, new EquatableArray<string>(ImmutableArray.Create(messageArgs)));
}
