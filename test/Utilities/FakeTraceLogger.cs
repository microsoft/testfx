// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.UnitTests;

/// <summary>
/// A fake logger that can be used across AppDomain boundaries.
/// Moq proxies are not serializable and cannot cross AppDomain boundaries.
/// </summary>
[Serializable]
internal sealed class FakeTraceLogger : MarshalByRefObject, IAdapterTraceLogger
{
    public bool IsInfoEnabled => false;

    public bool IsWarningEnabled => false;

    public bool IsErrorEnabled => false;

    public bool IsVerboseEnabled => false;

    public void LogError(string format, params object?[] args)
    {
    }

    public void LogWarning(string format, params object?[] args)
    {
    }

    public void LogInfo(string format, params object?[] args)
    {
    }

    public void LogVerbose(string format, params object?[] args)
    {
    }

    public void PrepareRemoteAppDomain(AppDomain appDomain)
    {
    }
}
#endif
