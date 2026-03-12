// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestClass]
public class RunContextAdapterTests
{
    private readonly Mock<ICommandLineOptions> _commandLineOptions = new();
    private readonly Mock<IRunSettings> _runSettings = new();

    private static readonly string DefaultRunSettings =
"""
        <RunSettings>
            <RunConfiguration>
            </RunConfiguration>
        </RunSettings>
""";

    [TestMethod]
    public void TestRunDirectory_IsNotNull_If_ResultsDirectory_Is_Provided()
    {
        string runSettings =
"""
        <RunSettings>
            <RunConfiguration>
                <ResultsDirectory>/PlatformResultDirectoryFromFile</ResultsDirectory>
            </RunConfiguration>
        </RunSettings>
""";

        _runSettings.Setup(x => x.SettingsXml).Returns(runSettings);
        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, new NopFilter());
        Assert.AreEqual("/PlatformResultDirectoryFromFile", runContextAdapter.TestRunDirectory);
        Assert.AreEqual(runSettings, runContextAdapter.RunSettings!.SettingsXml);
        Assert.IsNotNull(runContextAdapter.RunSettings);
    }

    [TestMethod]
    public void TestRunDirectory_IsNull_If_ResultsDirectory_IsNot_Provided()
    {
        string runSettings =
"""
        <RunSettings>
            <RunConfiguration>
            </RunConfiguration>
        </RunSettings>
""";

        _runSettings.Setup(x => x.SettingsXml).Returns(runSettings);
        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, new NopFilter());
        Assert.IsNull(runContextAdapter.TestRunDirectory);
        Assert.IsNotNull(runContextAdapter.RunSettings);
    }

    [TestMethod]
    public void GetTestCaseFilter_WithEscapedOpenParenthesis_DoesNotThrow()
    {
        // Regression test: When the filter contains an escaped open parenthesis (e.g., to filter parametrized tests by name),
        // the ContextAdapterBase wraps it as "(DisplayName~aaa \()" which previously caused a false
        // "Empty parenthesis ( )" error because the regex didn't account for the escape character.
        string filterExpression = @"DisplayName~aaa \(";

        _runSettings.Setup(x => x.SettingsXml).Returns(DefaultRunSettings);
        _commandLineOptions
            .Setup(x => x.TryGetOptionArgumentList(TestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName, out It.Ref<string[]?>.IsAny))
            .Returns((string _, out string[]? args) =>
            {
                args = [filterExpression];
                return true;
            });

        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, new NopFilter());

        // Should not throw even though the filter contains \( which when wrapped becomes (\()
        ITestCaseFilterExpression? filterExpr = runContextAdapter.GetTestCaseFilter(["DisplayName"], _ => null);
        Assert.IsNotNull(filterExpr);
    }

    [TestMethod]
    public void GetTestCaseFilter_WithEscapedParentheses_CorrectlyMatchesTests()
    {
        // Regression test: ensure that escaped parentheses in filter values are properly handled
        // when filtering parametrized tests by name prefix (e.g., "aaa" vs "aaa2" where both have parameters)
        string filterExpression = @"DisplayName~aaa \(";

        _runSettings.Setup(x => x.SettingsXml).Returns(DefaultRunSettings);
        _commandLineOptions
            .Setup(x => x.TryGetOptionArgumentList(TestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName, out It.Ref<string[]?>.IsAny))
            .Returns((string _, out string[]? args) =>
            {
                args = [filterExpression];
                return true;
            });

        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, new NopFilter());

        TestProperty displayNameProperty = TestCaseProperties.DisplayName;
        ITestCaseFilterExpression? filterExpr = runContextAdapter.GetTestCaseFilter(["DisplayName"], _ => displayNameProperty);
        Assert.IsNotNull(filterExpr);

        // "aaa (1, 2)" should match the filter "DisplayName~aaa \(" because it contains "aaa ("
        TestCase matchingTestCase = new("MyNamespace.MyClass.aaa", new Uri("executor://uri"), "source.dll")
        {
            DisplayName = "aaa (1, 2)",
        };
        Assert.IsTrue(filterExpr.MatchTestCase(matchingTestCase, name => matchingTestCase.GetPropertyValue(displayNameProperty)));

        // "aaa2 (1, 2)" should NOT match because "aaa2 (" is different from "aaa ("
        TestCase nonMatchingTestCase = new("MyNamespace.MyClass.aaa2", new Uri("executor://uri"), "source.dll")
        {
            DisplayName = "aaa2 (1, 2)",
        };
        Assert.IsFalse(filterExpr.MatchTestCase(nonMatchingTestCase, name => nonMatchingTestCase.GetPropertyValue(displayNameProperty)));
    }

    [TestMethod]
    public void GetTestCaseFilter_WithActualEmptyParentheses_ThrowsFormatException()
    {
        // Ensure that actual empty parentheses "()" still produce the expected error
        string filterExpression = "DisplayName~aaa ()";

        _runSettings.Setup(x => x.SettingsXml).Returns(DefaultRunSettings);
        _commandLineOptions
            .Setup(x => x.TryGetOptionArgumentList(TestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName, out It.Ref<string[]?>.IsAny))
            .Returns((string _, out string[]? args) =>
            {
                args = [filterExpression];
                return true;
            });

        RunContextAdapter runContextAdapter = new(_commandLineOptions.Object, _runSettings.Object, new NopFilter());

        // Should throw because "()" is genuinely empty parentheses
        TestPlatformFormatException ex = Assert.ThrowsExactly<TestPlatformFormatException>(() => runContextAdapter.GetTestCaseFilter(["DisplayName"], _ => null));
        StringAssert.Contains(ex.Message, "Empty parenthesis");
    }
}
