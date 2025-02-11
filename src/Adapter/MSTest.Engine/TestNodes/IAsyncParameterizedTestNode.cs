// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

#if NET
namespace Microsoft.Testing.Framework;

internal interface IAsyncParameterizedTestNode : IExpandableTestNode
{
    Func<IAsyncEnumerable<object>> GetArguments { get; }
}
#endif
