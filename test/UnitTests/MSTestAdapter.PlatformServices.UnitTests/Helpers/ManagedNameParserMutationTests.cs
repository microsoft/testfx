// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

// ParseManagedMethodName validates the end position of the whole name afterwards, so a parsing step that fails to
// reject its own input or to report what it captured is only visible by driving the individual steps.
public sealed class ManagedNameParserMutationTests : TestContainer
{
    private delegate int ParseParameterTypeListDelegate(string managedMethodName, int start, out string[]? parameterTypes);

    private delegate int ParseParameterTypeDelegate(string managedMethodName, int start, out string parameterType);

#if DEBUG && !NETFRAMEWORK
    private delegate int ParseArityDelegate(string managedMethodName, int start, out int arity);
#endif

    public void ParseParameterTypeList_RejectsAListThatIsNeverClosed()
    {
        ParseParameterTypeListDelegate parseParameterTypeList = GetStep<ParseParameterTypeListDelegate>("ParseParameterTypeList");

        foreach (string unterminated in new[] { "Method(", "Method(int", "Method(int,", "Method(int,string" })
        {
            Action parse = () => parseParameterTypeList(unterminated, 6, out _);

            parse.Should().Throw<InvalidManagedNameException>(because: $"'{unterminated}' has no closing parenthesis");
        }
    }

    public void ParseParameterType_CapturesNothingWhenTheTextEndsBeforeAnyDelimiter()
    {
        ParseParameterTypeDelegate parseParameterType = GetStep<ParseParameterTypeDelegate>("ParseParameterType");

        int end = parseParameterType("Method(int", 7, out string parameterType);

        end.Should().Be(10, because: "the unterminated type extends to the end of the text");
        parameterType.Should().BeEmpty(because: "only a ',' or ')' delimiter completes a parameter type");
    }

#if DEBUG && !NETFRAMEWORK
    public void ParseArity_ReportsATextThatDoesNotStartWithABacktick()
    {
        ParseArityDelegate parseArity = GetStep<ParseArityDelegate>("ParseArity");
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        int end = parseArity("x5(", 0, out int arity);

        assertions.Should().Equal(["Expected arity to start with a backtick."], because: "an arity has to be introduced by a backtick");
        end.Should().Be(2, because: "the arity ends at the opening parenthesis");
        arity.Should().Be(5, because: "the digits after the first character are the arity");
    }

    public void ParseParameterTypeList_ReportsATextThatDoesNotStartWithAnOpenParenthesis()
    {
        ParseParameterTypeListDelegate parseParameterTypeList = GetStep<ParseParameterTypeListDelegate>("ParseParameterTypeList");
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        int end = parseParameterTypeList("Mx)", 1, out string[]? parameterTypes);

        assertions.Should().Equal(["Expected parameter type list to start with open paren."], because: "a parameter type list has to start with an open parenthesis");
        end.Should().Be(3, because: "the list ends after the closing parenthesis");
        parameterTypes.Should().BeNull(because: "the list holds no parameter type");
    }
#endif

    private static TDelegate GetStep<TDelegate>(string methodName)
        where TDelegate : Delegate
        => (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), typeof(ManagedNameParser).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!);
}
