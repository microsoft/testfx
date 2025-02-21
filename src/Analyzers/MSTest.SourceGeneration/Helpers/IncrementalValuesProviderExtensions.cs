// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Framework.SourceGeneration.Helpers;

internal static class IncrementalValuesProviderExtensions
{
    private static readonly Func<object?, bool> NotNullTest = x => x != null;

    public static IncrementalValuesProvider<T> WhereNotNull<T>(this IncrementalValuesProvider<T?> source)
        where T : class
        => source.Where(NotNullTest)!;
}
