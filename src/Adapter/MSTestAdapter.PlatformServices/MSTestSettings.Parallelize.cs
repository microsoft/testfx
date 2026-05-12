// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed partial class MSTestSettings
{
    private static void SetParallelSettings(XmlReader reader, MSTestSettings settings)
    {
        reader.Read();
        if (!reader.IsEmptyElement)
        {
            reader.Read();

            while (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "WORKERS":
                        string workers = reader.ReadInnerXml();
                        settings.ParallelizationWorkers = int.TryParse(workers, out int parallelWorkers)
                            ? parallelWorkers == 0
                                ? Environment.ProcessorCount
                                : parallelWorkers > 0
                                    ? parallelWorkers
                                    : throw new AdapterSettingsException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelWorkersValue, workers))
                            : throw new AdapterSettingsException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelWorkersValue, workers));
                        break;
                    case "SCOPE":
                        string scopeValue = reader.ReadInnerXml();
                        settings.ParallelizationScope = TryParseEnum(scopeValue, out ExecutionScope scope)
                            ? scope
                            : throw new AdapterSettingsException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelScopeValue, scopeValue, string.Join(", ", Enum.GetNames(typeof(ExecutionScope)))));
                        break;
                    default:
                        throw new AdapterSettingsException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidSettingsXmlElement, reader.Name, ParallelizeSettingsName));
                }
            }
        }

        settings.ParallelizationWorkers ??= Environment.ProcessorCount;
        settings.ParallelizationScope ??= ExecutionScope.ClassLevel;
    }
}
