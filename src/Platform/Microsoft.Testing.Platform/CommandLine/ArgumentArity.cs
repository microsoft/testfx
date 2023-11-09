// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.CommandLine;

// https://learn.microsoft.com/dotnet/standard/commandline/syntax#argument-arity
public record struct ArgumentArity(int Min, int Max)
{
    public static readonly ArgumentArity Zero = new(0, 0);
    public static readonly ArgumentArity ZeroOrOne = new(0, 1);
    public static readonly ArgumentArity ZeroOrMore = new(0, int.MaxValue);
    public static readonly ArgumentArity OneOrMore = new(1, int.MaxValue);
    public static readonly ArgumentArity ExactlyOne = new(1, 1);
}
