// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.CommandLine;

/// <summary>
/// Represents the arity (minimum and maximum number of arguments) for a command line argument.
/// </summary>
/// <remarks>
/// This is taken from https://learn.microsoft.com/dotnet/standard/commandline/syntax#argument-arity.
/// </remarks>
public readonly struct ArgumentArity(int min, int max) : IEquatable<ArgumentArity>
{
    /// <summary>
    /// Represents an argument arity of zero.
    /// </summary>
    public static readonly ArgumentArity Zero = new(0, 0);

    /// <summary>
    /// Represents an argument arity of zero or one.
    /// </summary>
    public static readonly ArgumentArity ZeroOrOne = new(0, 1);

    /// <summary>
    /// Represents an argument arity of zero or more.
    /// </summary>
    public static readonly ArgumentArity ZeroOrMore = new(0, int.MaxValue);

    /// <summary>
    /// Represents an argument arity of one or more.
    /// </summary>
    public static readonly ArgumentArity OneOrMore = new(1, int.MaxValue);

    /// <summary>
    /// Represents an argument arity of exactly one.
    /// </summary>
    public static readonly ArgumentArity ExactlyOne = new(1, 1);

    /// <summary>
    /// Gets the minimum number of arguments.
    /// </summary>
    public int Min { get; } = min;

    /// <summary>
    /// Gets the maximum number of arguments.
    /// </summary>
    public int Max { get; } = max;

    /// <summary>
    /// Determines whether two instances of <see cref="ArgumentArity"/> are equal.
    /// </summary>
    /// <param name="left">The first <see cref="ArgumentArity"/> to compare.</param>
    /// <param name="right">The second <see cref="ArgumentArity"/> to compare.</param>
    /// <returns><c>true</c> if the instances are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(ArgumentArity left, ArgumentArity right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two instances of <see cref="ArgumentArity"/> are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="ArgumentArity"/> to compare.</param>
    /// <param name="right">The second <see cref="ArgumentArity"/> to compare.</param>
    /// <returns><c>true</c> if the instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(ArgumentArity left, ArgumentArity right)
        => !(left == right);

    /// <inheritdoc/>
    public bool Equals(ArgumentArity other)
        => Min == other.Min && Max == other.Max;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is ArgumentArity argumentArity && Equals(argumentArity);

    /// <inheritdoc/>
    public override int GetHashCode() =>
#if NET
        HashCode.Combine(Min, Max);
#else
        Min.GetHashCode() ^ Max.GetHashCode();
#endif

}
