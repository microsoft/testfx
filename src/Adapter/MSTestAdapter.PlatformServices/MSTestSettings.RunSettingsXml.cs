// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using DebuggerLaunchMode = Microsoft.VisualStudio.TestTools.UnitTesting.DebuggerLaunchMode;
using MessageLevel = Microsoft.VisualStudio.TestTools.UnitTesting.MessageLevel;
using StringEx = Microsoft.VisualStudio.TestTools.UnitTesting.StringEx;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed partial class MSTestSettings
{
    public static void PopulateSettings(MSTestSettings settings)
    {
        CurrentSettings.AssemblyCleanupTimeout = settings.AssemblyCleanupTimeout;
        CurrentSettings.AssemblyInitializeTimeout = settings.AssemblyInitializeTimeout;
        CurrentSettings.CaptureDebugTraces = settings.CaptureDebugTraces;
        CurrentSettings.ClassCleanupTimeout = settings.ClassCleanupTimeout;
        CurrentSettings.ClassInitializeTimeout = settings.ClassInitializeTimeout;
        CurrentSettings.ConsiderEmptyDataSourceAsInconclusive = settings.ConsiderEmptyDataSourceAsInconclusive;
        CurrentSettings.CooperativeCancellationTimeout = settings.CooperativeCancellationTimeout;
        CurrentSettings.DisableParallelization = settings.DisableParallelization;
        CurrentSettings.MapInconclusiveToFailed = settings.MapInconclusiveToFailed;
        CurrentSettings.MapNotRunnableToFailed = settings.MapNotRunnableToFailed;
        CurrentSettings.OrderTestsByNameInClass = settings.OrderTestsByNameInClass;
        CurrentSettings.RandomizeTestOrder = settings.RandomizeTestOrder;
        CurrentSettings.RandomTestOrderSeed = settings.RandomTestOrderSeed;
        CurrentSettings.ParallelizationScope = settings.ParallelizationScope;
        CurrentSettings.ParallelizationWorkers = settings.ParallelizationWorkers;
        CurrentSettings.TestCleanupTimeout = settings.TestCleanupTimeout;
        CurrentSettings.TestInitializeTimeout = settings.TestInitializeTimeout;
        CurrentSettings.TestTimeout = settings.TestTimeout;
        CurrentSettings.TreatDiscoveryWarningsAsErrors = settings.TreatDiscoveryWarningsAsErrors;
        CurrentSettings.LaunchDebuggerOnAssertionFailure = settings.LaunchDebuggerOnAssertionFailure;
    }

#if !WINDOWS_UWP
    private static bool RunSettingsFileHasMSTestSettings(string? runSettingsXml)
    {
        if (StringEx.IsNullOrWhiteSpace(runSettingsXml))
        {
            return false;
        }

        using var stringReader = new StringReader(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);

        XmlReaderUtilities.ReadToRootNode(reader);
        reader.ReadToNextElement();

        while (!string.Equals(reader.Name, SettingsName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(reader.Name, SettingsNameAlias, StringComparison.OrdinalIgnoreCase)
            && !reader.EOF)
        {
            reader.SkipToNextElement();
        }

        return !reader.EOF;
    }
#endif

    internal static MSTestSettings? GetSettings(string? runSettingsXml, string settingName, IAdapterMessageLogger? logger)
    {
        if (StringEx.IsNullOrWhiteSpace(runSettingsXml))
        {
            return null;
        }

        using var stringReader = new StringReader(runSettingsXml);
        var reader = XmlReader.Create(stringReader, RunSettingsUtilities.ReaderSettings);

        XmlReaderUtilities.ReadToRootNode(reader);
        reader.ReadToNextElement();

        while (!string.Equals(reader.Name, settingName, StringComparison.OrdinalIgnoreCase) && !reader.EOF)
        {
            reader.SkipToNextElement();
        }

        return !reader.EOF ? ToSettings(reader.ReadSubtree(), logger) : null;
    }

    private static MSTestSettings ToSettings(XmlReader reader, IAdapterMessageLogger? logger)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        MSTestSettings settings = new();
        reader.ReadToNextElement();

        if (!reader.IsEmptyElement)
        {
            reader.Read();
            while (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "CAPTURETRACEOUTPUT":
                        ParseBoolSetting(reader.ReadInnerXml(), "CaptureTraceOutput", logger, v => settings.CaptureDebugTraces = v);
                        break;
                    case "MAPINCONCLUSIVETOFAILED":
                        ParseBoolSetting(reader.ReadInnerXml(), "MapInconclusiveToFailed", logger, v => settings.MapInconclusiveToFailed = v);
                        break;
                    case "MAPNOTRUNNABLETOFAILED":
                        ParseBoolSetting(reader.ReadInnerXml(), "MapNotRunnableToFailed", logger, v => settings.MapNotRunnableToFailed = v);
                        break;
                    case "TREATDISCOVERYWARNINGSASERRORS":
                        ParseBoolSetting(reader.ReadInnerXml(), "TreatDiscoveryWarningsAsErrors", logger, v => settings.TreatDiscoveryWarningsAsErrors = v);
                        break;
                    case "PARALLELIZE":
                        SetParallelSettings(reader.ReadSubtree(), settings);
                        reader.SkipToNextElement();
                        break;
                    case "TESTTIMEOUT":
                        ParseTimeoutSetting(reader.ReadInnerXml(), "TestTimeout", logger, v => settings.TestTimeout = v);
                        break;
                    case "ASSEMBLYCLEANUPTIMEOUT":
                        ParseTimeoutSetting(reader.ReadInnerXml(), "AssemblyCleanupTimeout", logger, v => settings.AssemblyCleanupTimeout = v);
                        break;
                    case "CONSIDEREMPTYDATASOURCEASINCONCLUSIVE":
                        ParseBoolSetting(reader.ReadInnerXml(), "ConsiderEmptyDataSourceAsInconclusive", logger, v => settings.ConsiderEmptyDataSourceAsInconclusive = v);
                        break;
                    case "ASSEMBLYINITIALIZETIMEOUT":
                        ParseTimeoutSetting(reader.ReadInnerXml(), "AssemblyInitializeTimeout", logger, v => settings.AssemblyInitializeTimeout = v);
                        break;
                    case "CLASSINITIALIZETIMEOUT":
                        ParseTimeoutSetting(reader.ReadInnerXml(), "ClassInitializeTimeout", logger, v => settings.ClassInitializeTimeout = v);
                        break;
                    case "CLASSCLEANUPTIMEOUT":
                        ParseTimeoutSetting(reader.ReadInnerXml(), "ClassCleanupTimeout", logger, v => settings.ClassCleanupTimeout = v);
                        break;
                    case "TESTINITIALIZETIMEOUT":
                        ParseTimeoutSetting(reader.ReadInnerXml(), "TestInitializeTimeout", logger, v => settings.TestInitializeTimeout = v);
                        break;
                    case "TESTCLEANUPTIMEOUT":
                        ParseTimeoutSetting(reader.ReadInnerXml(), "TestCleanupTimeout", logger, v => settings.TestCleanupTimeout = v);
                        break;
                    case "COOPERATIVECANCELLATIONTIMEOUT":
                        ParseBoolSetting(reader.ReadInnerXml(), "CooperativeCancellationTimeout", logger, v => settings.CooperativeCancellationTimeout = v);
                        break;
                    case "ORDERTESTSBYNAMEINCLASS":
                        ParseBoolSetting(reader.ReadInnerXml(), "OrderTestsByNameInClass", logger, v => settings.OrderTestsByNameInClass = v);
                        break;
                    case "RANDOMIZETESTORDER":
                        ParseBoolSetting(reader.ReadInnerXml(), "RandomizeTestOrder", logger, v => settings.RandomizeTestOrder = v);
                        break;
                    case "RANDOMTESTORDERSEED":
                        string randomTestOrderSeed = reader.ReadInnerXml();
                        if (int.TryParse(randomTestOrderSeed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedRandomTestOrderSeed))
                        {
                            settings.RandomTestOrderSeed = parsedRandomTestOrderSeed;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, randomTestOrderSeed, "RandomTestOrderSeed"));
                        }

                        break;
                    case "LAUNCHDEBUGGERONASSERTIONFAILURE":
                        string launchDebuggerOnAssertionFailure = reader.ReadInnerXml();
                        if (TryParseEnum(launchDebuggerOnAssertionFailure, out DebuggerLaunchMode mode))
                        {
                            settings.LaunchDebuggerOnAssertionFailure = mode;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, launchDebuggerOnAssertionFailure, "LaunchDebuggerOnAssertionFailure"));
                        }

                        break;
                    default:
                        PlatformServiceProvider.Instance.SettingsProvider.Load(reader.ReadSubtree());
                        reader.SkipToNextElement();
                        break;
                }
            }
        }

        return settings;
    }

    private static void ParseBoolSetting(string rawValue, string settingName, IAdapterMessageLogger? logger, Action<bool> setSetting)
    {
        if (bool.TryParse(rawValue, out bool result))
        {
            setSetting(result);
        }
        else
        {
            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, rawValue, settingName));
        }
    }

    private static void ParseTimeoutSetting(string rawValue, string settingName, IAdapterMessageLogger? logger, Action<int> setSetting)
    {
        if (int.TryParse(rawValue, out int result) && result > 0)
        {
            setSetting(result);
        }
        else
        {
            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidTimeoutValue, rawValue, settingName));
        }
    }

    private static bool TryParseEnum<T>(string value, out T result)
        where T : struct, Enum
        => Enum.TryParse(value, true, out result)
#if NETCOREAPP
        && Enum.IsDefined(result);
#else
        && Enum.IsDefined(typeof(T), result);
#endif
}
