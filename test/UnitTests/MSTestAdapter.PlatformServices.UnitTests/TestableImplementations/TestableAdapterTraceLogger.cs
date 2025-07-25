// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace MSTestAdapter.PlatformServices.UnitTests.TestableImplementations;

[Serializable]
internal sealed class TestableAdapterTraceLogger : IAdapterTraceLogger
{
    private readonly Dictionary<string, List<string>> _logs = new()
    {
        ["error"] = [],
        ["info"] = [],
        ["verbose"] = [],
        ["warning"] = [],
    };

    public bool IsInfoEnabled => true;

    public void LogError(string format, params object?[] args) => _logs["error"].Add(string.Format(CultureInfo.CurrentCulture, format, args));

    public void LogInfo(string format, params object?[] args) => _logs["info"].Add(string.Format(CultureInfo.CurrentCulture, format, args));

    public void LogVerbose(string format, params object?[] args) => _logs["verbose"].Add(string.Format(CultureInfo.CurrentCulture, format, args));

    public void LogWarning(string format, params object?[] args) => _logs["warning"].Add(string.Format(CultureInfo.CurrentCulture, format, args));
}
