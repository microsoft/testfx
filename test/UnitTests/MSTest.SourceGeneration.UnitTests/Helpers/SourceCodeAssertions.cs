// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.Helpers;

internal sealed class SourceCodeAssertions : StringAssertions<SourceCodeAssertions>
{
    public SourceCodeAssertions(string value, AssertionChain assertionChain)
        : base(value, assertionChain)
    {
    }

    public AndConstraint<SourceCodeAssertions> ContainSourceCode(string expectedSourceCode, string because = "", params object[] becauseArgs)
    {
        if (string.IsNullOrEmpty(expectedSourceCode))
        {
            throw new ArgumentException("Cannot assert string containment against <null> or Empty source code.", nameof(expectedSourceCode));
        }

        bool onlyDifferInWhitespace = false;
        try
        {
            Subject.ShowReducedWhitespace().Should().Contain(expectedSourceCode.ShowReducedWhitespace());
            onlyDifferInWhitespace = true;
        }
        catch
        {
        }

        string subMessage = "Expected \n{context:string}\n{0}\n to contain\n{1}\n{reason}.";
        string message = onlyDifferInWhitespace
            ? $"WHITESPACE ONLY DIFFERENCE!\n\n{subMessage}"
            : subMessage;

        string actual = Subject.ShowWhitespace();
        string expected = expectedSourceCode.ShowWhitespace();
        CurrentAssertionChain
            .ForCondition(Contains(actual, expected, StringComparison.Ordinal))
            .BecauseOf(because, becauseArgs)
            .FailWith(message, actual, expected);

        return new AndConstraint<SourceCodeAssertions>(this);
    }

    public AndConstraint<SourceCodeAssertions> BeSourceCode(string expectedSourceCode, string because = "", params object[] becauseArgs)
    {
        if (string.IsNullOrEmpty(expectedSourceCode))
        {
            throw new ArgumentException("Cannot assert string equality against <null> or Empty source code.", nameof(expectedSourceCode));
        }

        bool onlyDifferInWhitespace = false;
        try
        {
            Subject.ShowReducedWhitespace().Should().Be(expectedSourceCode.ShowReducedWhitespace());
            onlyDifferInWhitespace = true;
        }
        catch
        {
        }

        string subMessage = "Expected \n{context:string}\n{0}\n to match\n{1}\n{reason}.";
        string message = onlyDifferInWhitespace
            ? $"WHITESPACE ONLY DIFFERENCE!\n\n{subMessage}"
            : subMessage;

        string actual = Subject.ShowWhitespace();
        string expected = expectedSourceCode.ShowWhitespace();

        CurrentAssertionChain
            .ForCondition(string.Equals(actual, expected, StringComparison.Ordinal))
            .BecauseOf(because, becauseArgs)
            .FailWith(message, actual, expected);

        return new AndConstraint<SourceCodeAssertions>(this);
    }

    private static bool Contains(string actual, string expected, StringComparison comparison)
        => (actual ?? string.Empty).Contains(expected ?? string.Empty, comparison);
}
