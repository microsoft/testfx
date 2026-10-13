// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class ManagedNameParserTests : TestContainer
{
    public void ParseManagedMethodName_DistinguishesNoParametersEmptyListAndNumericArity()
    {
        Verify("Method", "Method", 0, null);
        Verify("Method()", "Method", 0, null);
        Verify("Method`2", "Method", 2, null);
        Verify("Method`12(System.Int32,System.String)", "Method", 12, ["System.Int32", "System.String"]);
        Verify("Method`0()", "Method", 0, null);
    }

    public void ParseManagedMethodName_QuotedNamesCanContainGrammarCharactersAndEscapes()
    {
        Verify("'method name'()", "method name", 0, null);
        Verify("'name(with,grammar)`text'(System.Int32)", "name(with,grammar)`text", 0, ["System.Int32"]);
        Verify("'escaped\\'quote'()", "escaped'quote", 0, null);
        Verify("'unicode\\u0041'()", "unicodeA", 0, null);
        Verify("prefix'quoted segment'suffix()", "prefixquoted segmentsuffix", 0, null);
    }

    public void ParseManagedMethodName_NestedGenericsAndArraysDoNotSplitTheirCommas()
    {
        Verify(
            "Method(System.Collections.Generic.Dictionary<System.String,System.Collections.Generic.List<System.Int32>>,System.Int32[,],System.String[])",
            "Method", 0,
            ["System.Collections.Generic.Dictionary<System.String,System.Collections.Generic.List<System.Int32>>", "System.Int32[,]", "System.String[]"]);
        Verify("Method('type with space',Generic<'quoted argument'>,Array['quoted rank'])", "Method", 0,
            ["'type with space'", "Generic<'quoted argument'>", "Array['quoted rank']"]);
    }

    public void ParseManagedMethodName_RejectsInvalidArityWhitespaceAndTrailingText()
    {
        foreach (string text in new[]
        {
            "bad name()", "Method`abc()", "Method`()", "Method`999999999999999999999()",
            "Method(System. Int32)", "Method(Type[ ])", "Method(Generic< >)", "Method()trailing",
        })
        {
            Action action = () => ManagedNameParser.ParseManagedMethodName(text, out _, out _, out _);
            action.Should().Throw<InvalidManagedNameException>($"'{text}' is invalid managed-name syntax");
        }
    }

    public void ParseManagedMethodName_RejectsIncompleteQuotedGenericArrayAndParameterLists()
    {
        foreach (string text in new[]
        {
            "'unfinished", "Method(", "Method(System.Int32", "Method(Type[)", "Method(Generic<System.String)",
            "Method(Generic<Nested<System.String>)", "Method('unfinished)", "Method(Generic<'unfinished>)",
            "Method(Type['unfinished])", "'bad\\uZZZZ'()", "'short\\u12'()",
        })
        {
            Action action = () => ManagedNameParser.ParseManagedMethodName(text, out _, out _, out _);
            action.Should().Throw<InvalidManagedNameException>($"'{text}' is incomplete or malformed");
        }
    }

    public void ParseEscapedString_PreservesUnquotedTextAndCombinesQuotedSegments()
    {
        ManagedNameHelper.ParseEscapedString("ordinary").Should().Be("ordinary");
        ManagedNameHelper.ParseEscapedString("prefix'first'between'second'tail").Should().Be("prefixfirstbetweensecondtail");
        ManagedNameHelper.ParseEscapedString("'back\\\\slash'").Should().Be("back\\slash");
        ManagedNameHelper.ParseEscapedString("'\\u03B1'").Should().Be("α");
    }

    private static void Verify(string input, string expectedName, int expectedArity, string[]? expectedParameters)
    {
        ManagedNameParser.ParseManagedMethodName(input, out string name, out int arity, out string[]? parameters);
        name.Should().Be(expectedName);
        arity.Should().Be(expectedArity);
        if (expectedParameters is null)
        {
            parameters.Should().BeNull();
        }
        else
        {
            parameters.Should().Equal(expectedParameters);
        }
    }
}
