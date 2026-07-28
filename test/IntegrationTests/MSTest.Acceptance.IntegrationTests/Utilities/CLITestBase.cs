// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation;

public abstract partial class CLITestBase
{
    private const string DefaultRunSettingsXml = """
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors />
  </DataCollectionRunSettings>
</RunSettings>
""";

    protected static string GetRunSettingsXml(string settingsXml)
        => string.IsNullOrEmpty(settingsXml) ? DefaultRunSettingsXml : settingsXml;
}
