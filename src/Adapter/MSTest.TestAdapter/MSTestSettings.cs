// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Testing.Platform.Configurations;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Adapter Settings for the run.
/// </summary>
[Serializable]
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
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
        OrderTestsByNameInClass = false;
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
    /// Gets a value indicating whether tests should be ordered by name in the class.
    /// </summary>
    internal bool OrderTestsByNameInClass { get; private set; }

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

        CurrentSettings.AssemblyCleanupTimeout = settings.AssemblyCleanupTimeout;
        CurrentSettings.AssemblyInitializeTimeout = settings.AssemblyInitializeTimeout;
        CurrentSettings.CaptureDebugTraces = settings.CaptureDebugTraces;
        CurrentSettings.ClassCleanupLifecycle = settings.ClassCleanupLifecycle;
        CurrentSettings.ClassCleanupTimeout = settings.ClassCleanupTimeout;
        CurrentSettings.ClassInitializeTimeout = settings.ClassInitializeTimeout;
        CurrentSettings.ConsiderEmptyDataSourceAsInconclusive = settings.ConsiderEmptyDataSourceAsInconclusive;
        CurrentSettings.ConsiderFixturesAsSpecialTests = settings.ConsiderFixturesAsSpecialTests;
        CurrentSettings.CooperativeCancellationTimeout = settings.CooperativeCancellationTimeout;
        CurrentSettings.DisableParallelization = settings.DisableParallelization;
        CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies = settings.EnableBaseClassTestMethodsFromOtherAssemblies;
        CurrentSettings.ForcedLegacyMode = settings.ForcedLegacyMode;
        CurrentSettings.MapInconclusiveToFailed = settings.MapInconclusiveToFailed;
        CurrentSettings.MapNotRunnableToFailed = settings.MapNotRunnableToFailed;
        CurrentSettings.OrderTestsByNameInClass = settings.OrderTestsByNameInClass;
        CurrentSettings.ParallelizationScope = settings.ParallelizationScope;
        CurrentSettings.ParallelizationWorkers = settings.ParallelizationWorkers;
        CurrentSettings.TestCleanupTimeout = settings.TestCleanupTimeout;
        CurrentSettings.TestInitializeTimeout = settings.TestInitializeTimeout;
        CurrentSettings.TestSettingsFile = settings.TestSettingsFile;
        CurrentSettings.TestTimeout = settings.TestTimeout;
        CurrentSettings.TreatClassAndAssemblyCleanupWarningsAsErrors = settings.TreatClassAndAssemblyCleanupWarningsAsErrors;
        CurrentSettings.TreatDiscoveryWarningsAsErrors = settings.TreatDiscoveryWarningsAsErrors;
    }

    /// <summary>
    /// Populate adapter settings from the context.
    /// </summary>
    /// <param name="context">
    /// The discovery context that contains the runsettings.
    /// </param>
    [Obsolete("this function will be removed in v4.0.0")]
    public static void PopulateSettings(IDiscoveryContext? context) => PopulateSettings(context, null, null);

    private static bool IsRunSettingsFileHasMSTestSettings(string? runSettingsXml) => IsRunSettingsFileHasSettingName(runSettingsXml, SettingsName) || IsRunSettingsFileHasSettingName(runSettingsXml, SettingsNameAlias);

    private static bool IsRunSettingsFileHasSettingName(string? runSettingsXml, string SettingName)
    {
        if (StringEx.IsNullOrWhiteSpace(runSettingsXml))
        {
            return false;
        }

        using var stringReader = new StringReader(runSettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

        // read to the fist child
        XmlReaderUtilities.ReadToRootNode(reader);
        reader.ReadToNextElement();

        // Read till we reach nodeName element or reach EOF
        while (!string.Equals(reader.Name, SettingName, StringComparison.OrdinalIgnoreCase)
                && !reader.EOF)
        {
            reader.SkipToNextElement();
        }

        return !reader.EOF;
    }

    /// <summary>
    /// Populate adapter settings from the context.
    /// </summary>
    /// <param name="context">
    /// <param name="logger"> The logger for messages. </param>
    /// The discovery context that contains the runsettings.
    /// </param>
    internal static void PopulateSettings(IDiscoveryContext? context, IMessageLogger? logger, IConfiguration? configuration)
    {
        if (configuration?["mstest"] != null && context?.RunSettings != null && IsRunSettingsFileHasMSTestSettings(context.RunSettings.SettingsXml))
        {
            throw new InvalidOperationException(Resource.DuplicateConfigurationError);
        }

        // This will contain default adapter settings
        var settings = new MSTestSettings();
        var runConfigurationSettings = RunConfigurationSettings.PopulateSettings(context);

        if (!StringEx.IsNullOrEmpty(context?.RunSettings?.SettingsXml) && configuration?["mstest"] is null)
        {
            MSTestSettings? aliasSettings = GetSettings(context.RunSettings.SettingsXml, SettingsNameAlias, logger);

            // If a user specifies MSTestV2 in the runsettings, then prefer that over the v1 settings.
            if (aliasSettings != null)
            {
                settings = aliasSettings;
            }
            else
            {
                MSTestSettings? mSTestSettings = GetSettings(context.RunSettings.SettingsXml, SettingsName, logger);

                settings = mSTestSettings ?? new MSTestSettings();
            }

            SetGlobalSettings(context.RunSettings.SettingsXml, settings, logger);
        }
        else if (configuration?["mstest"] is not null)
        {
            RunConfigurationSettings.SetRunConfigurationSettingsFromConfig(configuration, runConfigurationSettings);
            SetSettingsFromConfig(configuration, logger, settings);
        }

        CurrentSettings = settings;
        RunConfigurationSettings = runConfigurationSettings;
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
    /// <param name="logger"> The logger for messages. </param>
    /// <returns> The settings if found. Null otherwise. </returns>
    internal static MSTestSettings? GetSettings(
        [StringSyntax(StringSyntaxAttribute.Xml, nameof(runSettingsXml))] string? runSettingsXml,
        string settingName, IMessageLogger? logger)
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
            return ToSettings(reader.ReadSubtree(), logger);
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
    /// <param name="logger"> The logger for messages. </param>
    /// <returns>An instance of the <see cref="MSTestSettings"/> class.</returns>
    private static MSTestSettings ToSettings(XmlReader reader, IMessageLogger? logger)
    {
        Guard.NotNull(reader);

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
                            if (bool.TryParse(value, out result))
                            {
                                settings.CaptureDebugTraces = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "CaptureTraceOutput"));
                            }

                            break;
                        }

                    case "ENABLEBASECLASSTESTMETHODSFROMOTHERASSEMBLIES":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.EnableBaseClassTestMethodsFromOtherAssemblies = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "EnableBaseClassTestMethodsFromOtherAssemblies"));
                            }

                            break;
                        }

                    case "CLASSCLEANUPLIFECYCLE":
                        {
                            string value = reader.ReadInnerXml();
                            settings.ClassCleanupLifecycle = TryParseEnum(value, out ClassCleanupBehavior lifecycle)
                                ? lifecycle
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidClassCleanupLifecycleValue,
                                        value,
#if NET
                                        string.Join(", ", Enum.GetNames<ClassCleanupBehavior>())));
#else
                                        string.Join(", ", EnumPolyfill.GetNames<ClassCleanupBehavior>())));
#endif

                            break;
                        }

                    case "FORCEDLEGACYMODE":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.ForcedLegacyMode = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "ForcedLegacyMode"));
                            }

                            break;
                        }

                    case "MAPINCONCLUSIVETOFAILED":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.MapInconclusiveToFailed = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "MapInconclusiveToFailed"));
                            }

                            break;
                        }

                    case "MAPNOTRUNNABLETOFAILED":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.MapNotRunnableToFailed = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "MapNotRunnableToFailed"));
                            }

                            break;
                        }

                    case "TREATDISCOVERYWARNINGSASERRORS":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.TreatDiscoveryWarningsAsErrors = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "TreatDiscoveryWarningsAsErrors"));
                            }

                            break;
                        }

                    case "SETTINGSFILE":
                        {
                            string fileName = reader.ReadInnerXml();

                            if (!StringEx.IsNullOrEmpty(fileName))
                            {
                                settings.TestSettingsFile = fileName;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, fileName, "SettingsFile"));
                            }

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
                            if (int.TryParse(value, out int testTimeout) && testTimeout > 0)
                            {
                                settings.TestTimeout = testTimeout;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "TestTimeout"));
                            }

                            break;
                        }

                    case "ASSEMBLYCLEANUPTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();
                            if (int.TryParse(value, out int assemblyCleanupTimeout) && assemblyCleanupTimeout > 0)
                            {
                                settings.AssemblyCleanupTimeout = assemblyCleanupTimeout;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "AssemblyCleanupTimeout"));
                            }

                            break;
                        }

                    case "CONSIDEREMPTYDATASOURCEASINCONCLUSIVE":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out bool considerEmptyDataSourceAsInconclusive))
                            {
                                settings.ConsiderEmptyDataSourceAsInconclusive = considerEmptyDataSourceAsInconclusive;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "ConsiderEmptyDataSourceAsInconclusive"));
                            }

                            break;
                        }

                    case "ASSEMBLYINITIALIZETIMEOUT":
                        {
                            string value = reader.ReadInnerXml();
                            if (int.TryParse(value, out int assemblyInitializeTimeout) && assemblyInitializeTimeout > 0)
                            {
                                settings.AssemblyInitializeTimeout = assemblyInitializeTimeout;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "AssemblyInitializeTimeout"));
                            }

                            break;
                        }

                    case "CLASSINITIALIZETIMEOUT":
                        {
                            string value = reader.ReadInnerXml();
                            if (int.TryParse(value, out int classInitializeTimeout) && classInitializeTimeout > 0)
                            {
                                settings.ClassInitializeTimeout = classInitializeTimeout;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "ClassInitializeTimeout"));
                            }

                            break;
                        }

                    case "CLASSCLEANUPTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();
                            if (int.TryParse(value, out int classCleanupTimeout) && classCleanupTimeout > 0)
                            {
                                settings.ClassCleanupTimeout = classCleanupTimeout;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "ClassCleanupTimeout"));
                            }

                            break;
                        }

                    case "TESTINITIALIZETIMEOUT":
                        {
                            string value = reader.ReadInnerXml();
                            if (int.TryParse(value, out int testInitializeTimeout) && testInitializeTimeout > 0)
                            {
                                settings.TestInitializeTimeout = testInitializeTimeout;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "TestInitializeTimeout"));
                            }

                            break;
                        }

                    case "TESTCLEANUPTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();
                            if (int.TryParse(value, out int testCleanupTimeout) && testCleanupTimeout > 0)
                            {
                                settings.TestCleanupTimeout = testCleanupTimeout;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "TestCleanupTimeout"));
                            }

                            break;
                        }

                    case "TREATCLASSANDASSEMBLYCLEANUPWARNINGSASERRORS":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.TreatClassAndAssemblyCleanupWarningsAsErrors = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "TreatClassAndAssemblyCleanupWarningsAsErrors"));
                            }

                            break;
                        }

                    case "CONSIDERFIXTURESASSPECIALTESTS":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.ConsiderFixturesAsSpecialTests = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "ConsiderFixturesAsSpecialTests"));
                            }

                            break;
                        }

                    case "COOPERATIVECANCELLATIONTIMEOUT":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.CooperativeCancellationTimeout = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "CooperativeCancellationTimeout"));
                            }

                            break;
                        }

                    case "ORDERTESTSBYNAMEINCLASS":
                        {
                            string value = reader.ReadInnerXml();
                            if (bool.TryParse(value, out result))
                            {
                                settings.OrderTestsByNameInClass = result;
                            }
                            else
                            {
                                logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, "OrderTestsByNameInClass"));
                            }

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
                                ? scope
                                : throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidParallelScopeValue,
                                        value,
#if NET
                                        string.Join(", ", Enum.GetNames<ExecutionScope>())));
#else
                                        string.Join(", ", EnumPolyfill.GetNames<ExecutionScope>())));
#endif

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
        where T : struct, Enum
        => Enum.TryParse(value, true, out result)
#if NET6_0_OR_GREATER
        && Enum.IsDefined(result);
#else
        && Enum.IsDefined(typeof(T), result);
#endif

    private static void SetGlobalSettings(
        [StringSyntax(StringSyntaxAttribute.Xml, nameof(runsettingsXml))] string runsettingsXml,
        MSTestSettings settings, IMessageLogger? logger)
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
        else if (disableParallelizationString is not null)
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, disableParallelizationString, "DisableParallelization"));
        }
    }

    private static void ParseBooleanSetting(IConfiguration configuration, string key, IMessageLogger? logger, Action<bool> setSetting)
    {
        if (configuration[$"mstest:{key}"] is not string value)
        {
            return;
        }

        if (bool.TryParse(value, out bool result))
        {
            setSetting(result);
        }
        else
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, key));
        }
    }

    private static void ParseIntegerSetting(IConfiguration configuration, string key, IMessageLogger? logger, Action<int> setSetting)
    {
        if (configuration[$"mstest:{key}"] is not string value)
        {
            return;
        }

        if (int.TryParse(value, out int result) && result > 0)
        {
            setSetting(result);
        }
        else
        {
            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, value, key));
        }
    }

    /// <summary>
    /// Convert the parameter xml to TestSettings.
    /// </summary>
    /// <param name="configuration">Configuration to load the settings from.</param>
    /// <param name="logger"> The logger for messages. </param>
    internal static void SetSettingsFromConfig(IConfiguration configuration, IMessageLogger? logger, MSTestSettings settings)
    {
        // Expected format of the json is: -
        //
        // "mstest" : {
        //  "timeout" : {
        //      "assemblyInitialize" : strictly positive int,
        //      "assemblyCleanup" : strictly positive int,
        //      "classInitialize" : strictly positive int,
        //      "classCleanup" : strictly positive int,
        //      "testInitialize" : strictly positive int,
        //      "testCleanup" : strictly positive int,
        //      "test" : strictly positive int,
        //      "useCooperativeCancellation" : true/false
        //  },
        //  "parallelism" : {
        //      "enabled": true/false,
        //      "workers": positive int,
        //      "scope": method/class,
        //  },
        //  "output" : {
        //      "captureTrace" : true/false
        //  },
        //  "execution" : {
        //      "mapInconclusiveToFailed" : true/false
        //      "mapNotRunnableToFailed" : true/false
        //      "treatDiscoveryWarningsAsErrors" : true/false
        //      "considerEmptyDataSourceAsInconclusive" : true/false
        //      "treatClassAndAssemblyCleanupWarningsAsErrors" : true/false
        //      "considerFixturesAsSpecialTests" : true/false
        //  }
        //  ... remaining settings
        // }
        ParseBooleanSetting(configuration, "enableBaseClassTestMethodsFromOtherAssemblies", logger, value => settings.EnableBaseClassTestMethodsFromOtherAssemblies = value);
        ParseBooleanSetting(configuration, "orderTestsByNameInClass", logger, value => settings.OrderTestsByNameInClass = value);

        ParseBooleanSetting(configuration, "output:captureTrace", logger, value => settings.CaptureDebugTraces = value);

        ParseBooleanSetting(configuration, "parallelism:enabled", logger, value => settings.OrderTestsByNameInClass = value);

        ParseBooleanSetting(configuration, "execution:mapInconclusiveToFailed", logger, value => settings.MapInconclusiveToFailed = value);
        ParseBooleanSetting(configuration, "execution:mapNotRunnableToFailed", logger, value => settings.MapNotRunnableToFailed = value);
        ParseBooleanSetting(configuration, "execution:treatDiscoveryWarningsAsErrors", logger, value => settings.TreatDiscoveryWarningsAsErrors = value);
        ParseBooleanSetting(configuration, "execution:considerEmptyDataSourceAsInconclusive", logger, value => settings.ConsiderEmptyDataSourceAsInconclusive = value);
        ParseBooleanSetting(configuration, "execution:treatClassAndAssemblyCleanupWarningsAsErrors", logger, value => settings.TreatClassAndAssemblyCleanupWarningsAsErrors = value);
        ParseBooleanSetting(configuration, "execution:considerFixturesAsSpecialTests", logger, value => settings.ConsiderFixturesAsSpecialTests = value);

        ParseBooleanSetting(configuration, "timeout:useCooperativeCancellation", logger, value => settings.CooperativeCancellationTimeout = value);
        ParseIntegerSetting(configuration, "timeout:test", logger, value => settings.TestTimeout = value);
        ParseIntegerSetting(configuration, "timeout:assemblyCleanup", logger, value => settings.AssemblyCleanupTimeout = value);
        ParseIntegerSetting(configuration, "timeout:assemblyInitialize", logger, value => settings.AssemblyInitializeTimeout = value);
        ParseIntegerSetting(configuration, "timeout:classInitialize", logger, value => settings.ClassInitializeTimeout = value);
        ParseIntegerSetting(configuration, "timeout:classCleanup", logger, value => settings.ClassCleanupTimeout = value);
        ParseIntegerSetting(configuration, "timeout:testInitialize", logger, value => settings.TestInitializeTimeout = value);
        ParseIntegerSetting(configuration, "timeout:testCleanup", logger, value => settings.TestCleanupTimeout = value);

        if (configuration["mstest:classCleanupLifecycle"] is string classCleanupLifecycle)
        {
            if (TryParseEnum(classCleanupLifecycle, out ClassCleanupBehavior lifecycle))
            {
                settings.ClassCleanupLifecycle = lifecycle;
            }
            else
            {
                throw new AdapterSettingsException(string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.InvalidClassCleanupLifecycleValue,
                    classCleanupLifecycle,
#if NET
                    string.Join(", ", Enum.GetNames<ClassCleanupBehavior>())));
#else
                    string.Join(", ", EnumPolyfill.GetNames<ClassCleanupBehavior>())));
#endif
            }
        }

        if (configuration["mstest:parallelism:workers"] is string workers)
        {
            settings.ParallelizationWorkers = int.TryParse(workers, out int parallelWorkers)
                ? parallelWorkers == 0
                    ? Environment.ProcessorCount
                    : parallelWorkers > 0
                        ? parallelWorkers
                        : throw new AdapterSettingsException(string.Format(
                                                CultureInfo.CurrentCulture,
                                                Resource.InvalidParallelWorkersValue,
                                                workers))
                : throw new AdapterSettingsException(string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.InvalidParallelWorkersValue,
                    workers));
        }

        if (configuration["mstest:parallelism:scope"] is string value)
        {
            value = value.Equals("class", StringComparison.OrdinalIgnoreCase) ? "ClassLevel"
                    : value.Equals("methood", StringComparison.OrdinalIgnoreCase) ? "MethodLevel" : value;
            if (TryParseEnum(value, out ExecutionScope scope))
            {
                settings.ParallelizationScope = scope;
            }
            else
            {
                throw new AdapterSettingsException(string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.InvalidParallelScopeValue,
                    value,
#if NET
                    string.Join(", ", Enum.GetNames<ExecutionScope>())));
#else
                    string.Join(", ", EnumPolyfill.GetNames<ExecutionScope>())));
#endif
            }
        }

        MSTestSettingsProvider.Load(configuration);
    }
}
