// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Framework.SourceGeneration.Helpers;

internal static class EnumerableExtensions
{
    private static readonly Func<object?, bool> NotNullTest = x => x != null;

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
        => source.Where((Func<T?, bool>)NotNullTest)!;
}
