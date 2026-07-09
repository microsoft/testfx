// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

/// <summary>
/// Tests for the filter building logic in <see cref="ContextAdapterBase"/> (exercised through
/// <see cref="RunContextAdapter"/>), most notably the private <c>BuildFilter</c> method that turns a
/// <see cref="TestNodeUidListFilter"/> coming from the server protocol into a VSTest
/// <c>TestCaseFilter</c> expression.
/// </summary>
[TestClass]
public sealed class RunContextAdapterFilterTests
{
    private const string EmptyRunSettings =
"""
<RunSettings>
    <RunConfiguration>
    </RunConfiguration>
</RunSettings>
""";

    [TestMethod]
    public void GetTestCaseFilter_WithSecondNodeContainingPipe_KeepsBothNodesAndEscapesPipe()
    {
        // A later node whose name contains '|' must be emitted as an escaped literal ('\|') so it
        // stays part of its FullyQualifiedName value, while the '|' that joins the two clauses remains
        // the OR operator.
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("Namespace.MyClass.MyTest", "PrintArg(\"as|\")"));
        string res = GetFilterValue(adapter);

        Assert.AreEqual("(FullyQualifiedName=Namespace.MyClass.MyTest|FullyQualifiedName=PrintArg\\(\"as\\|\"\\))", res);
    }

    [TestMethod]
    public void GetTestCaseFilter_WithNopFilter_AndNoRunSettingsOrCommandLineFilter_ReturnsNull()
    {
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, new NopFilter());

        Assert.IsNull(adapter.GetTestCaseFilter(null, _ => null));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithSingleFullyQualifiedNameNode_BuildsFullyQualifiedNameFilter()
    {
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("Namespace.MyClass.MyTest"));

        Assert.AreEqual("(FullyQualifiedName=Namespace.MyClass.MyTest)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithEmptyNodeList_ThrowsOnEmptyGroup()
    {
        // An empty UID list is constructible and builds the empty group "()", which the VSTest filter
        // parser rejects, so GetTestCaseFilter surfaces a format error. Pinning this guards the edge
        // case now that this suite owns the filter-building coverage.
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter());

        TestPlatformFormatException exception = Assert.ThrowsExactly<TestPlatformFormatException>(() => adapter.GetTestCaseFilter(null, _ => null));
        Assert.AreEqual("()", exception.FilterValue);
    }

    [TestMethod]
    public void GetTestCaseFilter_WithMultipleNodes_JoinsWithOrOperator()
    {
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("A.B.Test1", "C.D.Test2"));

        Assert.AreEqual("(FullyQualifiedName=A.B.Test1|FullyQualifiedName=C.D.Test2)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithGuidNode_BuildsIdFilter()
    {
        var guid = new Guid("12345678-1234-1234-1234-1234567890ab");
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter(guid.ToString()));

        Assert.AreEqual($"(Id={guid})", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithMixedGuidAndNameNodes_BuildsIdAndFullyQualifiedNameFilters()
    {
        var guid = new Guid("12345678-1234-1234-1234-1234567890ab");
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter(guid.ToString(), "A.B.Test"));

        Assert.AreEqual($"(Id={guid}|FullyQualifiedName=A.B.Test)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithSpecialCharactersInName_EscapesFilterOperators()
    {
        // The pipe, parentheses, ampersand, equals, bang, tilde and backslash are all TestCaseFilter
        // operators and must be escaped so they are treated as literals (regression for tests whose
        // display name contains such characters, e.g. NUnit [TestCase("as|")]).
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("Ns.PrintArg(\"as|\")"));

        Assert.AreEqual("(FullyQualifiedName=Ns.PrintArg\\(\"as\\|\"\\))", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithAllOperatorCharacters_EscapesEachOfThem()
    {
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("a\\b(c)d&e|f=g!h~i"));

        Assert.AreEqual("(FullyQualifiedName=a\\\\b\\(c\\)d\\&e\\|f\\=g\\!h\\~i)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithRunSettingsTestCaseFilter_UsesRunSettingsFilter()
    {
        string runSettings =
"""
<RunSettings>
    <RunConfiguration>
        <TestCaseFilter>Category=Fast</TestCaseFilter>
    </RunConfiguration>
</RunSettings>
""";
        RunContextAdapter adapter = CreateAdapter(runSettings, new NopFilter());

        Assert.AreEqual("(Category=Fast)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithCommandLineFilter_UsesCommandLineFilter()
    {
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, new NopFilter(), commandLineFilter: "Category=Slow");

        Assert.AreEqual("(Category=Slow)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithRunSettingsAndNodeFilter_CombinesWithAndOperator()
    {
        string runSettings =
"""
<RunSettings>
    <RunConfiguration>
        <TestCaseFilter>Category=Fast</TestCaseFilter>
    </RunConfiguration>
</RunSettings>
""";
        RunContextAdapter adapter = CreateAdapter(runSettings, CreateUidFilter("A.B.Test"));

        Assert.AreEqual("(Category=Fast) & (FullyQualifiedName=A.B.Test)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithRunSettingsAndCommandLineFilter_CombinesWithAndOperator()
    {
        string runSettings =
"""
<RunSettings>
    <RunConfiguration>
        <TestCaseFilter>Category=Fast</TestCaseFilter>
    </RunConfiguration>
</RunSettings>
""";
        RunContextAdapter adapter = CreateAdapter(runSettings, new NopFilter(), commandLineFilter: "Priority=1");

        Assert.AreEqual("(Category=Fast) & (Priority=1)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithSingleNodeStartingWithSpecialCharacter_DoesNotThrowAndEscapes()
    {
        // Companion to the multi-node regression above, exhaustive over the index dimension: the
        // single-node case (i == 0) never threw under the old code because the "i - 1 < 0" guard
        // short-circuited before the bogus Value[k - 1] read. It must still escape the leading operator.
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("(weird"));

        Assert.AreEqual("(FullyQualifiedName=\\(weird)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithSecondNodeStartingWithSpecialCharacter_DoesNotThrowAndEscapes()
    {
        // Regression: the "already-escaped" guard in BuildFilter used the node index (i) instead of
        // the character index (k) when looking back at the previous character. For any node after the
        // first (i > 0) whose first character (k == 0) is a filter operator, this evaluated
        // Value[k - 1] == Value[-1] and threw IndexOutOfRangeException.
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("A.B.Test", "(weird"));

        Assert.AreEqual("(FullyQualifiedName=A.B.Test|FullyQualifiedName=\\(weird)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithOperator_EscapesOperator()
    {
        // A bare operator ('|') in the name must be escaped so it stays a literal instead of being
        // parsed as an OR that splits the clause.
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("A.B|C"));

        Assert.AreEqual("(FullyQualifiedName=A.B\\|C)", GetFilterValue(adapter));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithBackslashFollowedBySpecialCharacter_EscapesBoth()
    {
        // Regression: the buggy "already-escaped" guard treated the operator following a literal
        // backslash as if it were already escaped, so it emitted the operator un-escaped. A raw
        // backslash in the name must be escaped to "\\" AND the following operator must still be
        // escaped, otherwise the operator (e.g. '|') is parsed as an OR and the clause is split.
        RunContextAdapter adapterBackslash = CreateAdapter(EmptyRunSettings, CreateUidFilter("A.B\\|C"));

        Assert.AreEqual("(FullyQualifiedName=A.B\\\\\\|C)", GetFilterValue(adapterBackslash));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithSpecialCharactersInName_RoundTripsAndMatchesOnlyExactName()
    {
        // Verify the full escape -> parse -> match path (not just the emitted string): a name full of
        // filter operators must, once escaped, parse back to a filter that matches a test case whose
        // FullyQualifiedName is exactly that name and rejects any other name.
        const string name = "Ns.PrintArg(\"as|\")";
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter(name));

        Assert.IsTrue(MatchesFullyQualifiedName(adapter, name));
        Assert.IsFalse(MatchesFullyQualifiedName(adapter, "Ns.PrintArg(\"as\")"));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithBackslashFollowedBySpecialCharacter_RoundTripsAndMatchesOnlyExactName()
    {
        // Regression guard for the round trip: if the backslash-then-operator sequence were under-escaped
        // the parser would split the value at the operator, so the filter would no longer match the exact
        // name (and might match a truncated one).
        const string name = "A.B\\|C";
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter(name));

        Assert.IsTrue(MatchesFullyQualifiedName(adapter, name));
        Assert.IsFalse(MatchesFullyQualifiedName(adapter, "A.B"));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithMultipleNodes_MatchesEitherNodeButNotOthers()
    {
        // The '|' joining the two clauses must stay an OR operator while a '|' inside a name stays a
        // literal, so the filter matches each node's exact name (including the special-character one) and
        // nothing else.
        RunContextAdapter adapter = CreateAdapter(EmptyRunSettings, CreateUidFilter("A.B.Test1", "PrintArg(\"as|\")"));

        Assert.IsTrue(MatchesFullyQualifiedName(adapter, "A.B.Test1"));
        Assert.IsTrue(MatchesFullyQualifiedName(adapter, "PrintArg(\"as|\")"));
        Assert.IsFalse(MatchesFullyQualifiedName(adapter, "A.B.Test2"));
    }

    private static TestNodeUidListFilter CreateUidFilter(params string[] uids)
        => new([.. uids.Select(uid => new TestNodeUid(uid))]);

    private static string GetFilterValue(RunContextAdapter adapter)
    {
        ITestCaseFilterExpression? filterExpression = adapter.GetTestCaseFilter(null, _ => null);
        Assert.IsNotNull(filterExpression);
        return filterExpression.TestCaseFilterValue;
    }

    private static bool MatchesFullyQualifiedName(RunContextAdapter adapter, string fullyQualifiedName)
    {
        ITestCaseFilterExpression? filterExpression = adapter.GetTestCaseFilter(null, _ => null);
        Assert.IsNotNull(filterExpression);

        var testCase = new TestCase(fullyQualifiedName, new Uri("executor://mstest"), "source.dll");
        return filterExpression.MatchTestCase(
            testCase,
            propertyName => string.Equals(propertyName, "FullyQualifiedName", StringComparison.OrdinalIgnoreCase)
                ? fullyQualifiedName
                : null);
    }

    private static RunContextAdapter CreateAdapter(string runSettingsXml, ITestExecutionFilter filter, string? commandLineFilter = null)
    {
        var runSettings = new Mock<IRunSettings>();
        runSettings.Setup(x => x.SettingsXml).Returns(runSettingsXml);

        var commandLineOptions = new Mock<ICommandLineOptions>();
        string[]? commandLineFilterArguments = commandLineFilter is null ? null : [commandLineFilter];
        commandLineOptions
            .Setup(x => x.TryGetOptionArgumentList(TestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName, out commandLineFilterArguments))
            .Returns(commandLineFilter is not null);

        return new RunContextAdapter(commandLineOptions.Object, runSettings.Object, filter);
    }
}
