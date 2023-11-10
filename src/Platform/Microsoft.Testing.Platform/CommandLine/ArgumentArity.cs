// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.CommandLine;

// https://learn.microsoft.com/dotnet/standard/commandline/syntax#argument-arity
public readonly struct ArgumentArity(int min, int max) : IEquatable<ArgumentArity>
{
    public static readonly ArgumentArity Zero = new(0, 0);
    public static readonly ArgumentArity ZeroOrOne = new(0, 1);
    public static readonly ArgumentArity ZeroOrMore = new(0, int.MaxValue);
    public static readonly ArgumentArity OneOrMore = new(1, int.MaxValue);
    public static readonly ArgumentArity ExactlyOne = new(1, 1);

    public int Min { get; } = min;

    public int Max { get; } = max;

    public bool Equals(ArgumentArity other)
        => Min == other.Min && Max == other.Max;

    public override bool Equals(object? obj)
        => obj is ArgumentArity argumentArity && Equals(argumentArity);

    public static bool operator ==(ArgumentArity left, ArgumentArity right)
        => left.Equals(right);

    public static bool operator !=(ArgumentArity left, ArgumentArity right)
        => !(left == right);

    public override int GetHashCode()
    {
#if NET
        return HashCode.Combine(Min, Max);
#else
        return Min.GetHashCode() ^ Max.GetHashCode();
#endif
    }
}
