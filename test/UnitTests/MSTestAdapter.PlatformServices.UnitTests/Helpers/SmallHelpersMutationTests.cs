// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class SmallHelpersMutationTests : TestContainer
{
    public void ReadToRootNode_RejectsARootElementOtherThanRunSettingsAndNamesTheElementItFound()
    {
        using StringReader stringReader = new("<NotRunSettings><Child /></NotRunSettings>");
        using var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);

        Action readToRootNode = () => XmlReaderUtilities.ReadToRootNode(reader);

        readToRootNode.Should().Throw<AdapterSettingsException>()
            .Which.Message.Should().Be(
                "Could not find 'RunSettings' node in the runsettings XML. Found '<NotRunSettings>' instead.",
                because: "the message has to tell which root element was found instead of <RunSettings>");
    }

    public void ReaderSettings_IgnoresCommentsAndWhitespace()
    {
        RunSettingsUtilities.ReaderSettings.IgnoreComments.Should().BeTrue(because: "comments are not part of the settings");
        RunSettingsUtilities.ReaderSettings.IgnoreWhitespace.Should().BeTrue(because: "indentation is not part of the settings");
    }

    public void GetTestRunParameters_ReadsTheParametersOfAnIndentedDocumentContainingComments()
    {
        const string RunSettings = """
            <?xml version="1.0" encoding="utf-8"?>
            <!-- leading comment -->
            <RunSettings>
              <!-- comment before the first element -->
              <RunConfiguration>
                <MaxCpuCount>1</MaxCpuCount>
              </RunConfiguration>
              <TestRunParameters>
                <!-- comment between the parameters -->
                <Parameter name="webAppUrl" value="http://localhost" />
                <Parameter Name="userName" Value="admin" />
              </TestRunParameters>
            </RunSettings>
            """;

        Dictionary<string, object>? parameters = RunSettingsUtilities.GetTestRunParameters(RunSettings);

        parameters.Should().Equal(
            new Dictionary<string, object> { ["webAppUrl"] = "http://localhost", ["userName"] = "admin" },
            because: "whitespace and comments between the elements must not stop the parameters from being read");
    }

    public void GetTestRunParameters_ReturnsNoParametersForAnEmptyElementAndLeavesTheNextSiblingAlone()
    {
        Dictionary<string, object>? parameters = RunSettingsUtilities.GetTestRunParameters(
            "<RunSettings><TestRunParameters /><MSTest><Parallelize /></MSTest></RunSettings>");

        parameters.Should().NotBeNull().And.BeEmpty(
            because: "an empty <TestRunParameters /> has no parameter, and the element that follows it is not one of its children");
    }

    public void ThrowOnHasAttributes_ReportsTheFirstAttributeInsteadOfTheElementName()
    {
        using StringReader stringReader = new("""<TestRunParameters first="1" second="2"><Parameter name="a" value="b" /></TestRunParameters>""");
        using var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);
        reader.MoveToContent();

        Action throwOnHasAttributes = () => RunSettingsUtilities.ThrowOnHasAttributes(reader);

        throwOnHasAttributes.Should().Throw<AdapterSettingsException>()
            .Which.Message.Should().Be(
                string.Format(CultureInfo.CurrentCulture, Resource.InvalidSettingsXmlAttribute, RunSettingsUtilities.RunConfigurationSettingsName, "first"),
                because: "the message names the unexpected attribute, not the element carrying it");

        Action getTestRunParameters = () => RunSettingsUtilities.GetTestRunParameters(
            """<RunSettings><TestRunParameters other="1"><Parameter name="a" value="b" /></TestRunParameters></RunSettings>""");

        getTestRunParameters.Should().Throw<AdapterSettingsException>()
            .Which.Message.Should().Be(
                string.Format(CultureInfo.CurrentCulture, Resource.InvalidSettingsXmlAttribute, RunSettingsUtilities.RunConfigurationSettingsName, "other"),
                because: "the same check applies while reading the test run parameters");
    }

    public void IsDataConsideredSingleArgumentValue_LooksForATupleRowOnlyWhenASingleValueIsSuppliedForADataAndTokenMethod()
    {
        ParameterInfo[] parameters = typeof(Action<object[], CancellationToken>).GetMethod(nameof(Action.Invoke))!.GetParameters();
        object[] values = [17, "array"];

        TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([], parameters)
            .Should().BeTrue(because: "no value is supplied, so there is no tuple row to look at");

        foreach (object tuple in new object[] { (values, CancellationToken.None), Tuple.Create(values, CancellationToken.None) })
        {
            TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([tuple], parameters)
                .Should().BeFalse(because: "a lone tuple supplying the data and its token is a full row");
            TestDataSourceHelpers.IsDataConsideredSingleArgumentValue([tuple, "extra"], parameters)
                .Should().BeTrue(because: "the tuple is not the single supplied value, so it is not unwrapped into a data and token row");
        }
    }

    public void RunOnContext_WithoutAnExecutionContextRunsTheActionOnTheCallingThread()
    {
        int invocations = 0;
        int actionThreadId = -1;

        ExecutionContextHelpers.RunOnContext(
            executionContext: null,
            () =>
            {
                invocations++;
                actionThreadId = Environment.CurrentManagedThreadId;
            });

        invocations.Should().Be(1, because: "the action has to run once even though there is no execution context to run it on");
        actionThreadId.Should().Be(Environment.CurrentManagedThreadId, because: "without an execution context the action runs inline");
    }

    public void ReflectHelperClearCache_DropsTheAttributesCachedByThePlatformReflectionOperations()
    {
        IPlatformServiceProvider previousProvider = PlatformServiceProvider.Instance;
        PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();
        try
        {
            Attribute[] cached = ReflectHelper.GetCustomAttributesCached(typeof(MarkedType));
            cached.Should().ContainSingle().Which.Should().BeOfType<MarkerAttribute>();
            ReflectHelper.GetCustomAttributesCached(typeof(MarkedType)).Should().BeSameAs(cached, because: "the attributes are served from the cache until it is cleared");

            ReflectHelper.ClearCache();

            Attribute[] reloaded = ReflectHelper.GetCustomAttributesCached(typeof(MarkedType));
            reloaded.Should().NotBeSameAs(cached, because: "clearing the cache forces the attributes to be read again");
            reloaded.Should().ContainSingle().Which.Should().BeOfType<MarkerAttribute>();
        }
        finally
        {
            PlatformServiceProvider.Instance = previousProvider;
        }
    }

#if DEBUG && !NETFRAMEWORK
    public void PerformActionOnAttribute_ReportsANullEntryOfTheAttributesArrayAndSkipsIt()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        var marker = new MarkerAttribute();
        var seen = new List<Attribute>();

        AttributeQueryHelper.PerformActionOnAttribute<MarkerAttribute, List<Attribute>>(
            [null!, marker],
            static (attribute, state) => state!.Add(attribute),
            seen);

        seen.Should().Equal([marker], because: "a null entry never matches the requested attribute type");
        assertions.Should().Equal(
            [$"AttributeQueryHelper: internal error: null entry in the attributes array while looking for '{typeof(MarkerAttribute)}'."],
            because: "a null entry in the attributes array is an internal error that has to be reported");
    }
#endif

    [AttributeUsage(AttributeTargets.All)]
    private sealed class MarkerAttribute : Attribute;

    [Marker]
    private sealed class MarkedType;
}
