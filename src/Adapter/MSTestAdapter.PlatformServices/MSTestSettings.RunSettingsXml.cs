// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

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
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

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
                bool result;
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "CAPTURETRACEOUTPUT":
                        string captureTraceOutput = reader.ReadInnerXml();
                        if (bool.TryParse(captureTraceOutput, out result))
                        {
                            settings.CaptureDebugTraces = result;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, captureTraceOutput, "CaptureTraceOutput"));
                        }

                        break;
                    case "MAPINCONCLUSIVETOFAILED":
                        string mapInconclusiveToFailed = reader.ReadInnerXml();
                        if (bool.TryParse(mapInconclusiveToFailed, out result))
                        {
                            settings.MapInconclusiveToFailed = result;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, mapInconclusiveToFailed, "MapInconclusiveToFailed"));
                        }

                        break;
                    case "MAPNOTRUNNABLETOFAILED":
                        string mapNotRunnableToFailed = reader.ReadInnerXml();
                        if (bool.TryParse(mapNotRunnableToFailed, out result))
                        {
                            settings.MapNotRunnableToFailed = result;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, mapNotRunnableToFailed, "MapNotRunnableToFailed"));
                        }

                        break;
                    case "TREATDISCOVERYWARNINGSASERRORS":
                        string treatDiscoveryWarningsAsErrors = reader.ReadInnerXml();
                        if (bool.TryParse(treatDiscoveryWarningsAsErrors, out result))
                        {
                            settings.TreatDiscoveryWarningsAsErrors = result;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, treatDiscoveryWarningsAsErrors, "TreatDiscoveryWarningsAsErrors"));
                        }

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
                        string considerEmptyDataSourceAsInconclusive = reader.ReadInnerXml();
                        if (bool.TryParse(considerEmptyDataSourceAsInconclusive, out bool parsedConsiderEmptyDataSourceAsInconclusive))
                        {
                            settings.ConsiderEmptyDataSourceAsInconclusive = parsedConsiderEmptyDataSourceAsInconclusive;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, considerEmptyDataSourceAsInconclusive, "ConsiderEmptyDataSourceAsInconclusive"));
                        }

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
                        string cooperativeCancellationTimeout = reader.ReadInnerXml();
                        if (bool.TryParse(cooperativeCancellationTimeout, out result))
                        {
                            settings.CooperativeCancellationTimeout = result;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, cooperativeCancellationTimeout, "CooperativeCancellationTimeout"));
                        }

                        break;
                    case "ORDERTESTSBYNAMEINCLASS":
                        string orderTestsByNameInClass = reader.ReadInnerXml();
                        if (bool.TryParse(orderTestsByNameInClass, out result))
                        {
                            settings.OrderTestsByNameInClass = result;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, orderTestsByNameInClass, "OrderTestsByNameInClass"));
                        }

                        break;
                    case "RANDOMIZETESTORDER":
                        string randomizeTestOrder = reader.ReadInnerXml();
                        if (bool.TryParse(randomizeTestOrder, out result))
                        {
                            settings.RandomizeTestOrder = result;
                        }
                        else
                        {
                            logger?.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, randomizeTestOrder, "RandomizeTestOrder"));
                        }

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
