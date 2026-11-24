// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace MSTestAdapter.PlatformServices.UnitTests;

public sealed class MSTestRunSettingsContinuationTests : TestContainer
{
    public void GetSettingsAbsentXmlAndSectionsDoNotFabricateSettings()
    {
        foreach (string? xml in new[]
        {
            null,
            string.Empty,
            " \t\r\n",
            "<RunSettings />",
            "<RunSettings><Other><MSTest><TestTimeout>91</TestTimeout></MSTest></Other></RunSettings>",
        })
        {
            MSTestSettings.GetSettings(xml, MSTestSettings.SettingsName, logger: null).Should().BeNull();
        }

        MSTestSettings settings = MSTestSettings.GetSettings(
            "<RunSettings><Other /><mStEsT /></RunSettings>", MSTestSettings.SettingsName, logger: null)!;
        settings.TestTimeout.Should().Be(0);
        settings.OutputCaptureMode.Should().Be(TestOutputCaptureMode.Result);
    }

    public void DuplicateConfigurationDetectsEitherCaseInsensitiveAdapterSection()
    {
        IConfiguration configuration = Configuration(new() { ["mstest"] = "present", ["mstest:timeout:test"] = "37" });
        MSTestSettings.PopulateSettings("<RunSettings><MSTest><TestTimeout>73</TestTimeout></MSTest></RunSettings>", null, null);
        MSTestSettings original = MSTestSettings.CurrentSettings;

        foreach (string section in new[] { "mStEsT", "MsTeStV2" })
        {
            string xml = $"<RunSettings><Other><Nested /></Other><!-- before adapter --><{section}><TestTimeout>19</TestTimeout></{section}></RunSettings>";
            Action load = () => MSTestSettings.PopulateSettings(xml, logger: null, configuration);

            load.Should().ThrowExactly<InvalidOperationException>().WithMessage(Resource.DuplicateConfigurationError);
            MSTestSettings.CurrentSettings.Should().BeSameAs(original);
            MSTestSettings.CurrentSettings.TestTimeout.Should().Be(73);
        }
    }

    public void JsonConfigurationIgnoresUnrelatedXmlSectionsAndEmptyXml()
    {
        IConfiguration configuration = Configuration(new() { ["mstest"] = "present", ["mstest:timeout:test"] = "37" });
        foreach (string? xml in new[]
        {
            null,
            string.Empty,
            "<RunSettings />",
            "<RunSettings><Other><Nested /></Other><RunConfiguration /></RunSettings>",
            "<RunSettings><Other><MSTest><TestTimeout>91</TestTimeout></MSTest></Other></RunSettings>",
        })
        {
            MSTestSettings.PopulateSettings(xml, logger: null, configuration);

            MSTestSettings.CurrentSettings.TestTimeout.Should().Be(37);
            MSTestSettings.CurrentSettings.OutputCaptureMode.Should().Be(TestOutputCaptureMode.Result);
        }
    }

    public void MalformedCombinedSettingsPreserveXmlFailureCause()
    {
        IConfiguration configuration = Configuration(new() { ["mstest"] = "present" });
        foreach (string xml in new[] { " \t\r\n", "<RunSettings><Other>" })
        {
            Action load = () => MSTestSettings.PopulateSettings(xml, logger: null, configuration);

            AdapterSettingsException exception = load.Should().ThrowExactly<AdapterSettingsException>().Subject.Single();
            exception.Message.Should().Be(Resource.InvalidSettingsXml);
            exception.InnerException.Should().BeOfType<XmlException>();
        }
    }

    public void DebuggerLaunchModeXmlAcceptsDefinedNamesAndWarnsForUnknownValues()
    {
        foreach ((string text, DebuggerLaunchMode expected) in new[]
        {
            ("Disabled", DebuggerLaunchMode.Disabled),
            ("eNaBlEd", DebuggerLaunchMode.Enabled),
            ("EnabledExcludingCI", DebuggerLaunchMode.EnabledExcludingCI),
            (" 1 ", DebuggerLaunchMode.Enabled),
        })
        {
            var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
            MSTestSettings settings = Parse($"<LaunchDebuggerOnAssertionFailure>{text}</LaunchDebuggerOnAssertionFailure>", logger.Object);

            settings.LaunchDebuggerOnAssertionFailure.Should().Be(expected);
            logger.VerifyNoOtherCalls();
        }

        foreach (string text in new[] { string.Empty, "unknown", "-1", "3", "Enabled, EnabledExcludingCI" })
        {
            var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
            string warning = string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, text, "LaunchDebuggerOnAssertionFailure");
            logger.Setup(value => value.SendMessage(MessageLevel.Warning, warning));

            MSTestSettings settings = Parse($"<LaunchDebuggerOnAssertionFailure>{text}</LaunchDebuggerOnAssertionFailure>", logger.Object);

            settings.LaunchDebuggerOnAssertionFailure.Should().Be(DebuggerLaunchMode.Disabled);
            logger.Verify(value => value.SendMessage(MessageLevel.Warning, warning), Times.Once);
            logger.VerifyNoOtherCalls();
            Parse($"<LaunchDebuggerOnAssertionFailure>{text}</LaunchDebuggerOnAssertionFailure>", null)
                .LaunchDebuggerOnAssertionFailure.Should().Be(DebuggerLaunchMode.Disabled);
        }
    }

    public void XmlSettingsObjectCopiesDistinctTimeoutsAndBehaviorFlags()
    {
        const string Xml = """
            <RunSettings>
              <RunConfiguration><DisableParallelization>true</DisableParallelization></RunConfiguration>
              <MSTest>
                <AssemblyCleanupTimeout>11</AssemblyCleanupTimeout>
                <AssemblyInitializeTimeout>13</AssemblyInitializeTimeout>
                <ClassCleanupTimeout>17</ClassCleanupTimeout>
                <ClassInitializeTimeout>19</ClassInitializeTimeout>
                <TestCleanupTimeout>23</TestCleanupTimeout>
                <TestInitializeTimeout>29</TestInitializeTimeout>
                <GlobalTestCleanupTimeout>31</GlobalTestCleanupTimeout>
                <GlobalTestInitializeTimeout>37</GlobalTestInitializeTimeout>
                <TestTimeout>2147483647</TestTimeout>
                <ConsiderEmptyDataSourceAsInconclusive>true</ConsiderEmptyDataSourceAsInconclusive>
                <CooperativeCancellationTimeout>true</CooperativeCancellationTimeout>
                <MapInconclusiveToFailed>true</MapInconclusiveToFailed>
                <MapNotRunnableToFailed>false</MapNotRunnableToFailed>
                <OrderTestsByNameInClass>true</OrderTestsByNameInClass>
                <CaptureTraceOutput>Live</CaptureTraceOutput>
                <RandomizeTestOrder>true</RandomizeTestOrder>
                <RandomTestOrderSeed>-2147483648</RandomTestOrderSeed>
                <Parallelize><Workers>3</Workers><Scope>MethodLevel</Scope></Parallelize>
                <TreatDiscoveryWarningsAsErrors>false</TreatDiscoveryWarningsAsErrors>
                <LaunchDebuggerOnAssertionFailure>EnabledExcludingCI</LaunchDebuggerOnAssertionFailure>
                <CaptureAssertionFailureDiagnostics>true</CaptureAssertionFailureDiagnostics>
              </MSTest>
            </RunSettings>
            """;
        MSTestSettings.PopulateSettings(Xml, logger: null, configuration: null);
        MSTestSettings source = MSTestSettings.CurrentSettings;
        MSTestSettings.Reset();
        MSTestSettings destination = MSTestSettings.CurrentSettings;

        MSTestSettings.PopulateSettings(source);

        MSTestSettings.CurrentSettings.Should().BeSameAs(destination).And.NotBeSameAs(source);
        destination.AssemblyCleanupTimeout.Should().Be(11);
        destination.AssemblyInitializeTimeout.Should().Be(13);
        destination.ClassCleanupTimeout.Should().Be(17);
        destination.ClassInitializeTimeout.Should().Be(19);
        destination.TestCleanupTimeout.Should().Be(23);
        destination.TestInitializeTimeout.Should().Be(29);
        destination.GlobalTestCleanupTimeout.Should().Be(31);
        destination.GlobalTestInitializeTimeout.Should().Be(37);
        destination.TestTimeout.Should().Be(int.MaxValue);
        destination.ConsiderEmptyDataSourceAsInconclusive.Should().BeTrue();
        destination.CooperativeCancellationTimeout.Should().BeTrue();
        destination.DisableParallelization.Should().BeTrue();
        destination.MapInconclusiveToFailed.Should().BeTrue();
        destination.MapNotRunnableToFailed.Should().BeFalse();
        destination.OrderTestsByNameInClass.Should().BeTrue();
        destination.OutputCaptureMode.Should().Be(TestOutputCaptureMode.Live);
        destination.RandomizeTestOrder.Should().BeTrue();
        destination.RandomTestOrderSeed.Should().Be(int.MinValue);
        destination.ParallelizationScope.Should().Be(ExecutionScope.MethodLevel);
        destination.ParallelizationWorkers.Should().Be(3);
        destination.TreatDiscoveryWarningsAsErrors.Should().BeFalse();
        destination.LaunchDebuggerOnAssertionFailure.Should().Be(DebuggerLaunchMode.EnabledExcludingCI);
        destination.CaptureAssertionFailureDiagnostics.Should().BeTrue();
    }

    public void InvalidCaptureModeXmlWarnsWithoutChangingDefault()
    {
        foreach ((string text, TestOutputCaptureMode expected) in new[]
        {
            ("true", TestOutputCaptureMode.Result),
            ("false", TestOutputCaptureMode.None),
            ("nOnE", TestOutputCaptureMode.None),
            ("Live", TestOutputCaptureMode.Live),
        })
        {
            Parse($"<CaptureTraceOutput>{text}</CaptureTraceOutput>", null).OutputCaptureMode.Should().Be(expected);
        }

        foreach (string text in new[] { "-1", "+2", " 2 ", "3", "9223372036854775808", "invalid-mode" })
        {
            var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
            string[] warnings = new[] { "CaptureTraceOutput", "MapNotRunnableToFailed", "CaptureAssertionFailureDiagnostics" }
                .Select(name => string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, text, name)).ToArray();
            foreach (string warning in warnings)
            {
                logger.Setup(value => value.SendMessage(MessageLevel.Warning, warning));
            }

            MSTestSettings settings = Parse(
                $"<CaptureTraceOutput>{text}</CaptureTraceOutput><MapNotRunnableToFailed>{text}</MapNotRunnableToFailed><CaptureAssertionFailureDiagnostics>{text}</CaptureAssertionFailureDiagnostics>",
                logger.Object);

            settings.OutputCaptureMode.Should().Be(TestOutputCaptureMode.Result);
            settings.MapNotRunnableToFailed.Should().BeTrue();
            settings.CaptureAssertionFailureDiagnostics.Should().BeFalse();
            foreach (string warning in warnings)
            {
                logger.Verify(value => value.SendMessage(MessageLevel.Warning, warning), Times.Once);
            }

            logger.VerifyNoOtherCalls();
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettings.Reset();
            MSTestSettingsProvider.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static MSTestSettings Parse(string content, IAdapterMessageLogger? logger)
        => MSTestSettings.GetSettings($"<RunSettings><MSTest>{content}</MSTest></RunSettings>", MSTestSettings.SettingsName, logger)!;

    private static IConfiguration Configuration(Dictionary<string, string> values)
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(value => value[It.IsAny<string>()])
            .Returns((string key) => values.TryGetValue(key, out string? value) ? value : null);
        return configuration.Object;
    }
}
