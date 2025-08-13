// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices;

[Microsoft.CodeAnalysis.Embedded]
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Event)]
internal sealed class TupleElementNamesAttribute : Attribute
{
    private readonly string?[] _transformNames;

    public TupleElementNamesAttribute(string?[] transformNames)
        => _transformNames = transformNames ?? throw new ArgumentNullException(nameof(transformNames));

    public IList<string?> TransformNames => _transformNames;
}
#endif
