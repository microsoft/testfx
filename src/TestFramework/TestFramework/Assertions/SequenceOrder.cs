// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Specifies whether sequence elements must appear in the same order, or in any order,
/// when comparing collections with <see cref="Assert.AreSequenceEqual{T}(IEnumerable{T}?, IEnumerable{T}?, SequenceOrder, string?, string, string)"/>.
/// </summary>
public enum SequenceOrder
{
    /// <summary>
    /// Elements must appear in the same order in both sequences (LINQ <see cref="System.Linq.Enumerable.SequenceEqual{TSource}(IEnumerable{TSource}, IEnumerable{TSource})"/> semantics).
    /// </summary>
    InOrder = 0,

    /// <summary>
    /// Elements may appear in any order, but each element must appear the same number of times in both sequences (multiset equality).
    /// </summary>
    InAnyOrder = 1,
}
