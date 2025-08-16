// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers.Lightup;

internal static class OperationWrapperHelper
{
    private static readonly Assembly CodeAnalysisAssembly = typeof(SyntaxNode).GetTypeInfo().Assembly;

    private static readonly ImmutableDictionary<Type, Type?> WrappedTypes = ImmutableDictionary.Create<Type, Type?>()
        .Add(typeof(IAttributeOperationWrapper), CodeAnalysisAssembly.GetType(IAttributeOperationWrapper.WrappedTypeName));

    /// <summary>
    /// Gets the type that is wrapped by the given wrapper.
    /// </summary>
    /// <param name = "wrapperType">Type of the wrapper for which the wrapped type should be retrieved.</param>
    /// <returns>The wrapped type, or <see langword="null"/> if there is no info.</returns>
    internal static Type? GetWrappedType(Type wrapperType)
        => WrappedTypes.TryGetValue(wrapperType, out Type? wrappedType) ? wrappedType : null;
}
