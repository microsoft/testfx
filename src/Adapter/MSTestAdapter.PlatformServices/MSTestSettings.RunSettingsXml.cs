// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

using DebuggerLaunchMode = Microsoft.VisualStudio.TestTools.UnitTesting.DebuggerLaunchMode;
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

    internal static MSTestSettings? GetSettings(string? runSettingsXml, string settingName, IMessageLogger? logger)
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

    private static MSTestSettings ToSettings(XmlReader reader, IMessageLogger? logger)
    {
        ArgumentNullException.ThrowIfNull(reader);

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
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, captureTraceOutput, "CaptureTraceOutput"));
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
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, mapInconclusiveToFailed, "MapInconclusiveToFailed"));
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
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, mapNotRunnableToFailed, "MapNotRunnableToFailed"));
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
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, treatDiscoveryWarningsAsErrors, "TreatDiscoveryWarningsAsErrors"));
                        }

                        break;
                    case "PARALLELIZE":
                        SetParallelSettings(reader.ReadSubtree(), settings);
                        reader.SkipToNextElement();
                        break;
                    case "TESTTIMEOUT":
                        string testTimeout = reader.ReadInnerXml();
                        if (int.TryParse(testTimeout, out int parsedTestTimeout) && parsedTestTimeout > 0)
                        {
                            settings.TestTimeout = parsedTestTimeout;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, testTimeout, "TestTimeout"));
                        }

                        break;
                    case "ASSEMBLYCLEANUPTIMEOUT":
                        string assemblyCleanupTimeout = reader.ReadInnerXml();
                        if (int.TryParse(assemblyCleanupTimeout, out int parsedAssemblyCleanupTimeout) && parsedAssemblyCleanupTimeout > 0)
                        {
                            settings.AssemblyCleanupTimeout = parsedAssemblyCleanupTimeout;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, assemblyCleanupTimeout, "AssemblyCleanupTimeout"));
                        }

                        break;
                    case "CONSIDEREMPTYDATASOURCEASINCONCLUSIVE":
                        string considerEmptyDataSourceAsInconclusive = reader.ReadInnerXml();
                        if (bool.TryParse(considerEmptyDataSourceAsInconclusive, out bool parsedConsiderEmptyDataSourceAsInconclusive))
                        {
                            settings.ConsiderEmptyDataSourceAsInconclusive = parsedConsiderEmptyDataSourceAsInconclusive;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, considerEmptyDataSourceAsInconclusive, "ConsiderEmptyDataSourceAsInconclusive"));
                        }

                        break;
                    case "ASSEMBLYINITIALIZETIMEOUT":
                        string assemblyInitializeTimeout = reader.ReadInnerXml();
                        if (int.TryParse(assemblyInitializeTimeout, out int parsedAssemblyInitializeTimeout) && parsedAssemblyInitializeTimeout > 0)
                        {
                            settings.AssemblyInitializeTimeout = parsedAssemblyInitializeTimeout;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, assemblyInitializeTimeout, "AssemblyInitializeTimeout"));
                        }

                        break;
                    case "CLASSINITIALIZETIMEOUT":
                        string classInitializeTimeout = reader.ReadInnerXml();
                        if (int.TryParse(classInitializeTimeout, out int parsedClassInitializeTimeout) && parsedClassInitializeTimeout > 0)
                        {
                            settings.ClassInitializeTimeout = parsedClassInitializeTimeout;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, classInitializeTimeout, "ClassInitializeTimeout"));
                        }

                        break;
                    case "CLASSCLEANUPTIMEOUT":
                        string classCleanupTimeout = reader.ReadInnerXml();
                        if (int.TryParse(classCleanupTimeout, out int parsedClassCleanupTimeout) && parsedClassCleanupTimeout > 0)
                        {
                            settings.ClassCleanupTimeout = parsedClassCleanupTimeout;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, classCleanupTimeout, "ClassCleanupTimeout"));
                        }

                        break;
                    case "TESTINITIALIZETIMEOUT":
                        string testInitializeTimeout = reader.ReadInnerXml();
                        if (int.TryParse(testInitializeTimeout, out int parsedTestInitializeTimeout) && parsedTestInitializeTimeout > 0)
                        {
                            settings.TestInitializeTimeout = parsedTestInitializeTimeout;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, testInitializeTimeout, "TestInitializeTimeout"));
                        }

                        break;
                    case "TESTCLEANUPTIMEOUT":
                        string testCleanupTimeout = reader.ReadInnerXml();
                        if (int.TryParse(testCleanupTimeout, out int parsedTestCleanupTimeout) && parsedTestCleanupTimeout > 0)
                        {
                            settings.TestCleanupTimeout = parsedTestCleanupTimeout;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, testCleanupTimeout, "TestCleanupTimeout"));
                        }

                        break;
                    case "COOPERATIVECANCELLATIONTIMEOUT":
                        string cooperativeCancellationTimeout = reader.ReadInnerXml();
                        if (bool.TryParse(cooperativeCancellationTimeout, out result))
                        {
                            settings.CooperativeCancellationTimeout = result;
                        }
                        else
                        {
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, cooperativeCancellationTimeout, "CooperativeCancellationTimeout"));
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
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, orderTestsByNameInClass, "OrderTestsByNameInClass"));
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
                            logger?.SendMessage(TestMessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, launchDebuggerOnAssertionFailure, "LaunchDebuggerOnAssertionFailure"));
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

    private static bool TryParseEnum<T>(string value, out T result)
        where T : struct, Enum
        => Enum.TryParse(value, true, out result)
#if NETCOREAPP
        && Enum.IsDefined(result);
#else
        && Enum.IsDefined(typeof(T), result);
#endif
}
