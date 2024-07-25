// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Adapter Settings for the run.
/// </summary>
[Serializable]
public class MSTestSettings
{
    /// <summary>
    /// The settings name.
    /// </summary>
    public const string SettingsName = "MSTest";

    /// <summary>
    /// The alias to the default settings name.
    /// </summary>
    public const string SettingsNameAlias = "MSTestV2";

    private const string ParallelizeSettingsName = "Parallelize";

    /// <summary>
    /// Member variable for Adapter settings.
    /// </summary>
    private static MSTestSettings? s_currentSettings;

    /// <summary>
    /// Member variable for RunConfiguration settings.
    /// </summary>
    private static RunConfigurationSettings? s_runConfigurationSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="MSTestSettings"/> class.
    /// </summary>
    public MSTestSettings()
    {
        CaptureDebugTraces = true;
        MapInconclusiveToFailed = false;
        MapNotRunnableToFailed = true;
        TreatDiscoveryWarningsAsErrors = false;
        EnableBaseClassTestMethodsFromOtherAssemblies = true;
        ForcedLegacyMode = false;
        TestSettingsFile = null;
        DisableParallelization = false;
        ConsiderEmptyDataSourceAsInconclusive = false;
        TestTimeout = 0;
        AssemblyInitializeTimeout = 0;
        ClassInitializeTimeout = 0;
        AssemblyCleanupTimeout = 0;
        ClassCleanupTimeout = 0;
        TestInitializeTimeout = 0;
        TestCleanupTimeout = 0;
        TreatClassAndAssemblyCleanupWarningsAsErrors = false;
        CooperativeCancellationTimeout = false;
    }

    /// <summary>
    /// Gets the current settings.
    /// </summary>
    [AllowNull]
    public static MSTestSettings CurrentSettings
    {
        get => s_currentSettings ??= new MSTestSettings();
        private set => s_currentSettings = value;
    }

    /// <summary>
    /// Gets the current configuration settings.
    /// </summary>
    [AllowNull]
    public static RunConfigurationSettings RunConfigurationSettings
    {
        get => s_runConfigurationSettings ??= new RunConfigurationSettings();
        private set => s_runConfigurationSettings = value;
    }

    /// <summary>
    /// Gets a value indicating whether capture debug traces.
    /// </summary>
    public bool CaptureDebugTraces { get; private set; }

    /// <summary>
    /// Gets a value indicating whether user wants the adapter to run in legacy mode or not.
    /// Default is False.
    /// </summary>
    public bool ForcedLegacyMode { get; private set; }

    /// <summary>
    /// Gets the path to settings file.
    /// </summary>
    public string? TestSettingsFile { get; private set; }

    /// <summary>
    /// Gets a value indicating whether an inconclusive result be mapped to failed test.
    /// </summary>
    public bool MapInconclusiveToFailed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a not runnable result be mapped to failed test.
    /// </summary>
    public bool MapNotRunnableToFailed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether or not test discovery warnings should be treated as errors.
    /// </summary>
    public bool TreatDiscoveryWarningsAsErrors { get; private set; }

    /// <summary>
    /// Gets a value indicating whether to enable discovery of test methods from base classes in a different assembly from the inheriting test class.
    /// </summary>
    public bool EnableBaseClassTestMethodsFromOtherAssemblies { get; private set; }

    /// <summary>
    /// Gets a value indicating where class cleanup should occur.
    /// </summary>
    public ClassCleanupBehavior? ClassCleanupLifecycle { get; private set; }

    /// <summary>
    /// Gets the number of threads/workers to be used for parallelization.
    /// </summary>
    public int? ParallelizationWorkers { get; private set; }

    /// <summary>
    /// Gets the scope of parallelization.
    /// </summary>
    public ExecutionScope? ParallelizationScope { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the assembly can be parallelized.
    /// </summary>
    /// <remarks>
    /// This is also re-used to disable parallelization on format errors.
    /// </remarks>
    public bool DisableParallelization { get; private set; }

    /// <summary>
    ///  Gets specified global test case timeout.
    /// </summary>
    public int TestTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global AssemblyInitialize timeout.
    /// </summary>
    internal int AssemblyInitializeTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global AssemblyCleanup timeout.
    /// </summary>
    internal int AssemblyCleanupTimeout { get; private set; }

    /// <summary>
    ///  Gets a value indicating whether to enable marking tests with missing dynamic data as Inconclusive.
    /// </summary>
    internal bool ConsiderEmptyDataSourceAsInconclusive { get; private set; }

    /// <summary>
    ///  Gets specified global ClassInitializeTimeout timeout.
    /// </summary>
    internal int ClassInitializeTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global ClassCleanupTimeout timeout.
    /// </summary>
    internal int ClassCleanupTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global TestInitializeTimeout timeout.
    /// </summary>
    internal int TestInitializeTimeout { get; private set; }

    /// <summary>
    ///  Gets specified global TestCleanupTimeout timeout.
    /// </summary>
    internal int TestCleanupTimeout { get; private set; }

    /// <summary>
    /// Gets a value indicating whether failures in class cleanups should be treated as errors.
    /// </summary>
    public bool TreatClassAndAssemblyCleanupWarningsAsErrors { get; private set; }

    /// <summary>
    /// Gets a value indicating whether AssemblyInitialize, AssemblyCleanup, ClassInitialize and ClassCleanup methods are
    /// reported as special tests (cannot be executed). When this feature is enabled, these methods will be reported as
    /// separate entries in the TRX reports, in Test Explorer or in CLI.
    /// </summary>
    internal bool ConsiderFixturesAsSpecialTests { get; private set; }

    /// <summary>
    /// Gets a value indicating whether all timeouts should be cooperative.
    /// </summary>
    internal bool CooperativeCancellationTimeout { get; private set; }

    /// <summary>
    /// Populate settings based on existing settings object.
    /// </summary>
    /// <param name="settings">The existing settings object.</param>
    public static void PopulateSettings(MSTestSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        CurrentSettings.CaptureDebugTraces = settings.CaptureDebugTraces;
        CurrentSettings.ForcedLegacyMode = settings.ForcedLegacyMode;
        CurrentSettings.TestSettingsFile = settings.TestSettingsFile;
        CurrentSettings.MapInconclusiveToFailed = settings.MapInconclusiveToFailed;
        CurrentSettings.MapNotRunnableToFailed = settings.MapNotRunnableToFailed;
        CurrentSettings.TreatDiscoveryWarningsAsErrors = settings.TreatDiscoveryWarningsAsErrors;
        CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies = settings.EnableBaseClassTestMethodsFromOtherAssemblies;
        CurrentSettings.ClassCleanupLifecycle = settings.ClassCleanupLifecycle;
        CurrentSettings.ParallelizationWorkers = settings.ParallelizationWorkers;
        CurrentSettings.ParallelizationScope = settings.ParallelizationScope;
        CurrentSettings.DisableParallelization = settings.DisableParallelization;
        CurrentSettings.TestTimeout = settings.TestTimeout;
        CurrentSettings.TreatClassAndAssemblyCleanupWarningsAsErrors = settings.TreatClassAndAssemblyCleanupWarningsAsErrors;
        CurrentSettings.AssemblyInitializeTimeout = settings.AssemblyInitializeTimeout;
        CurrentSettings.AssemblyCleanupTimeout = settings.AssemblyCleanupTimeout;
        CurrentSettings.ConsiderEmptyDataSourceAsInconclusive = settings.ConsiderEmptyDataSourceAsInconclusive;
        CurrentSettings.ClassInitializeTimeout = settings.ClassInitializeTimeout;
        CurrentSettings.ClassCleanupTimeout = settings.ClassCleanupTimeout;
        CurrentSettings.TestInitializeTimeout = settings.TestInitializeTimeout;
        CurrentSettings.TestCleanupTimeout = settings.TestCleanupTimeout;
        CurrentSettings.ConsiderFixturesAsSpecialTests = settings.ConsiderFixturesAsSpecialTests;
        CurrentSettings.CooperativeCancellationTimeout = settings.CooperativeCancellationTimeout;
    }

    /// <summary>
    /// Populate adapter settings from the context.
    /// </summary>
    /// <param name="context">
    /// The discovery context that contains the runsettings.
    /// </param>
    public static void PopulateSettings(IDiscoveryContext? context)
    {
        RunConfigurationSettings = RunConfigurationSettings.PopulateSettings(context);

        if (context?.RunSettings == null || StringEx.IsNullOrEmpty(context.RunSettings.SettingsXml))
        {
            // This will contain default adapter settings
            CurrentSettings = new MSTestSettings();
            return;
        }

        MSTestSettings? aliasSettings = GetSettings(context.RunSettings.SettingsXml, SettingsNameAlias);

        // If a user specifies MSTestV2 in the runsettings, then prefer that over the v1 settings.
        if (aliasSettings != null)
        {
            CurrentSettings = aliasSettings;
        }
        else
        {
            MSTestSettings? settings = GetSettings(context.RunSettings.SettingsXml, SettingsName);

            CurrentSettings = settings ?? new MSTestSettings();
        }

        SetGlobalSettings(context.RunSettings.SettingsXml, CurrentSettings);
    }

    /// <summary>
    /// Get the MSTestV1 adapter settings from the context.
    /// </summary>
    /// <param name="logger"> The logger for messages. </param>
    /// <returns> Returns true if test settings is provided.. </returns>
    public static bool IsLegacyScenario(IMessageLogger logger)
    {
        if (!StringEx.IsNullOrEmpty(CurrentSettings.TestSettingsFile))
        {
            logger.SendMessage(TestMessageLevel.Warning, Resource.LegacyScenariosNotSupportedWarning);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the adapter specific settings from the xml.
    /// </summary>
    /// <param name="runSettingsXml"> The xml with the settings passed from the test platform. </param>
    /// <param name="settingName"> The name of the adapter settings to fetch - Its either MSTest or MSTestV2. </param>
    /// <returns> The settings if found. Null otherwise. </returns>
    internal static MSTestSettings? GetSettings(
        [StringSyntax(StringSyntaxAttribute.Xml, nameof(runSettingsXml))] string? runSettingsXml,
        string settingName)
    {
        if (StringEx.IsNullOrWhiteSpace(runSettingsXml))
        {
            return null;
        }

        using var stringReader = new StringReader(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

        // read to the fist child
        XmlReaderUtilities.ReadToRootNode(reader);
        reader.ReadToNextElement();

        // Read till we reach nodeName element or reach EOF
        while (!string.Equals(reader.Name, settingName, StringComparison.OrdinalIgnoreCase)
                && !reader.EOF)
        {
            reader.SkipToNextElement();
        }

        if (!reader.EOF)
        {
            // read nodeName element.
            return ToSettings(reader.ReadSubtree());
        }

        return null;
    }

    /// <summary>
    /// Resets any settings loaded.
    /// </summary>
    internal static void Reset()
    {
        CurrentSettings = null;
        RunConfigurationSettings = null;
    }

    /// <summary>
    /// Convert the parameter xml to TestSettings.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    /// <returns>An instance of the <see cref="MSTestSettings"/> class.</returns>
    private static MSTestSettings ToSettings(XmlReader reader)
    {
        ValidateArg.NotNull(reader, "reader");

        // Expected format of the xml is: -
        //
        // <MSTestV2>
        //     <CaptureTraceOutput>true</CaptureTraceOutput>
        //     <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
        //     <MapNotRunnableToFailed>false</MapNotRunnableToFailed>
        //     <TreatDiscoveryWarningsAsErrors>false</TreatDiscoveryWarningsAsErrors>
        //     <EnableBaseClassTestMethodsFromOtherAssemblies>false</EnableBaseClassTestMethodsFromOtherAssemblies>
        //     <TestTimeout>5000</TestTimeout>
        //     <TreatClassAndAssemblyCleanupWarningsAsErrors>false</TreatClassAndAssemblyCleanupWarningsAsErrors>
        //     <Parallelize>
        //        <Workers>4</Workers>
        //        <Scope>TestClass</Scope>
        //     </Parallelize>
        // </MSTestV2>
        //
        // (or)
        //
        // <MSTest>
        //     <ForcedLegacyMode>true</ForcedLegacyMode>
        //     <SettingsFile>..\..\Local.testsettings</SettingsFile>
        //     <CaptureTraceOutput>true</CaptureTraceOutput>
        // </MSTest>
        MSTestSettings settings = new();

        // Read the first element in the section which is either "MSTest"/"MSTestV2"
        reader.ReadToNextElement();

        if (!reader.IsEmptyElement)
        {
            reader.Read();

            while (reader.NodeType == XmlNodeType.Element)
            {
                bool result;
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "CAPTURETRACEOUTPUT":
                        {
                            string value = reader.ReadInnerXml();
                            settings.CaptureDebugTraces = bool.TryParse(value, out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidCaptureTraceOutputValue,
                                        value));

                            break;
                        }

                    case "ENABLEBASECLASSTESTMETHODSFROMOTHERASSEMBLIES":
                        {
                            string value = reader.ReadInnerXml();

                            settings.EnableBaseClassTestMethodsFromOtherAssemblies = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidEnableBaseClassTestMethodsFromOtherAssembliesValue,
                                        value));

                            break;
                        }

                    case "CLASSCLEANUPLIFECYCLE":
                        {
                            string value = reader.ReadInnerXml();
                            settings.ClassCleanupLifecycle = TryParseEnum(value, out ClassCleanupBehavior lifecycle)
                                ? (ClassCleanupBehavior?)lifecycle
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidClassCleanupLifecycleValue,
                                        value));

                            break;
                        }

                    case "FORCEDLEGACYMODE":
                        {
                            string value = reader.ReadInnerXml();

                            settings.ForcedLegacyMode = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidForcedLegacyModeValue,
                                        value));

                            break;
                        }

                    case "MAPINCONCLUSIVETOFAILED":
                        {
                            string value = reader.ReadInnerXml();

                            settings.MapInconclusiveToFailed = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidMapInconclusiveToFailedValue,
                                        value));

                            break;
                        }

                    case "MAPNOTRUNNABLETOFAILED":
                        {
                            string value = reader.ReadInnerXml();

                            settings.MapNotRunnableToFailed = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidMapNotRunnableToFailedValue,
                                        value));

                            break;
                        }

                    case "TREATDISCOVERYWARNINGSASERRORS":
                        {
                            string value = reader.ReadInnerXml();

                            settings.TreatDiscoveryWarningsAsErrors = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidTreatDiscoveryWarningsAsErrorsValue,
                                        value));
                            break;
                        }

                    case "SETTINGSFILE":
                        {
                            string fileName = reader.ReadInnerXml();

                            settings.TestSettingsFile = StringEx.IsNullOrEmpty(fileName)
                                ? fileName
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidTestSettingsFileValue));

                            break;
                        }

                    case "PARALLELIZE":
                        {
                            SetParallelSettings(reader.ReadSubtree(), settings);
                            reader.SkipToNextElement();

                            break;
                        }

                    case "TESTTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.TestTimeout = int.TryParse(reader.ReadInnerXml(), out int testTimeout) && testTimeout > 0
                                ? testTimeout
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidTestTimeoutValue,
                                        value));
                            break;
                        }

                    case "ASSEMBLYCLEANUPTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.AssemblyCleanupTimeout = int.TryParse(reader.ReadInnerXml(), out int assemblyCleanupTimeout) && assemblyCleanupTimeout > 0
                                ? assemblyCleanupTimeout
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidAssemblyCleanupTimeoutValue,
                                        value));
                            break;
                        }

                    case "CONSIDEREMPTYDATASOURCEASINCONCLUSIVE":
                        {
                            string value = reader.ReadInnerXml();

                            settings.ConsiderEmptyDataSourceAsInconclusive = bool.TryParse(reader.ReadInnerXml(), out bool considerEmptyDataSourceAsInconclusive)
                                ? considerEmptyDataSourceAsInconclusive
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidConsiderEmptyDataSourceAsInconclusiveValue,
                                        value));

                            break;
                        }

                    case "ASSEMBLYINITIALIZETIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.AssemblyInitializeTimeout = int.TryParse(reader.ReadInnerXml(), out int assemblyInitializeTimeout) && assemblyInitializeTimeout > 0
                                ? assemblyInitializeTimeout
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidAssemblyInitializeTimeoutValue,
                                        value));
                            break;
                        }

                    case "CLASSINITIALIZETIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.ClassInitializeTimeout = int.TryParse(reader.ReadInnerXml(), out int classInitializeTimeout) && classInitializeTimeout > 0
                                ? classInitializeTimeout
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidClassInitializeTimeoutValue,
                                        value));
                            break;
                        }

                    case "CLASSCLEANUPTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.ClassCleanupTimeout = int.TryParse(reader.ReadInnerXml(), out int classCleanupTimeout) && classCleanupTimeout > 0
                                ? classCleanupTimeout
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidClassCleanupTimeoutValue,
                                        value));
                            break;
                        }

                    case "TESTINITIALIZETIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.TestInitializeTimeout = int.TryParse(reader.ReadInnerXml(), out int testInitializeTimeout) && testInitializeTimeout > 0
                                ? testInitializeTimeout
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidTestInitializeTimeoutValue,
                                        value));

                            break;
                        }

                    case "TESTCLEANUPTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.TestCleanupTimeout = int.TryParse(reader.ReadInnerXml(), out int testCleanupTimeout) && testCleanupTimeout > 0
                                ? testCleanupTimeout
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidTestCleanupTimeoutValue,
                                        value));
                            break;
                        }

                    case "TREATCLASSANDASSEMBLYCLEANUPWARNINGSASERRORS":
                        {
                            string value = reader.ReadInnerXml();

                            settings.TreatClassAndAssemblyCleanupWarningsAsErrors = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidTreatClassAndAssemblyCleanupWarningsAsErrorsValue,
                                        value));
                            break;
                        }

                    case "CONSIDERFIXTURESASSPECIALTESTS":
                        {
                            string value = reader.ReadInnerXml();

                            settings.ConsiderFixturesAsSpecialTests = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidConsiderFixturesAsSpecialTestsValue,
                                        value));
                            break;
                        }

                    case "COOPERATIVECANCELLATIONTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();

                            settings.CooperativeCancellationTimeout = bool.TryParse(reader.ReadInnerXml(), out result)
                                ? result
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidCooperativeCancellationTimeoutValue,
                                        value));

                            break;
                        }

                    default:
                        {
                            PlatformServiceProvider.Instance.SettingsProvider.Load(reader.ReadSubtree());
                            reader.SkipToNextElement();

                            break;
                        }
                }
            }
        }

        return settings;
    }

    private static void SetParallelSettings(XmlReader reader, MSTestSettings settings)
    {
        reader.Read();
        if (!reader.IsEmptyElement)
        {
            // Read the first child.
            reader.Read();

            while (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "WORKERS":
                        {
                            string value = reader.ReadInnerXml();
                            settings.ParallelizationWorkers = int.TryParse(value, out int parallelWorkers)
                                ? parallelWorkers == 0
                                    ? Environment.ProcessorCount
                                    : parallelWorkers > 0
                                        ? parallelWorkers
                                        : throw new AdapterSettingsException(string.Format(
                                            CultureInfo.CurrentCulture,
                                            Resource.InvalidParallelWorkersValue,
                                            value))
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidParallelWorkersValue,
                                        value));

                            break;
                        }

                    case "SCOPE":
                        {
                            string value = reader.ReadInnerXml();
                            settings.ParallelizationScope = TryParseEnum(value, out ExecutionScope scope)
                                ? (ExecutionScope?)scope
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidParallelScopeValue,
                                        value,
                                        string.Join(", ", Enum.GetNames(typeof(ExecutionScope)))));

                            break;
                        }

                    default:
                        {
                            throw new AdapterSettingsException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    Resource.InvalidSettingsXmlElement,
                                    ParallelizeSettingsName,
                                    reader.Name));
                        }
                }
            }
        }

        // If any of these properties are not set, resort to the defaults.
        settings.ParallelizationWorkers ??= Environment.ProcessorCount;

        settings.ParallelizationScope ??= ExecutionScope.ClassLevel;
    }

    private static bool TryParseEnum<T>(string value, out T result)
        where T : struct, Enum => Enum.TryParse(value, true, out result) && Enum.IsDefined(typeof(T), result);

    private static void SetGlobalSettings(
        [StringSyntax(StringSyntaxAttribute.Xml, nameof(runsettingsXml))] string runsettingsXml,
        MSTestSettings settings)
    {
        XElement? runConfigElement = XDocument.Parse(runsettingsXml).Element("RunSettings")?.Element("RunConfiguration");

        if (runConfigElement == null)
        {
            return;
        }

        string? disableParallelizationString = runConfigElement.Element("DisableParallelization")?.Value;
        if (bool.TryParse(disableParallelizationString, out bool disableParallelization))
        {
            settings.DisableParallelization = disableParallelization;
        }
    }
}
