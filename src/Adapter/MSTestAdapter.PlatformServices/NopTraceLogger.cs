// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed class NopTraceLogger : MarshalByRefObject, ITraceLogger
{
    private NopTraceLogger()
    {
    }

    public static ITraceLogger Instance { get; } = new NopTraceLogger();

    public bool IsVerboseEnabled => false;

    public bool IsInfoEnabled => false;

    public bool IsWarningEnabled => false;

    public bool IsErrorEnabled => false;

    public void Verbose(string format, params object?[] args)
    {
    }

    public void Verbose(string message)
    {
    }

    public void Info(string format, params object?[] args)
    {
    }

    public void Info(string message)
    {
    }

    public void Warning(string format, params object?[] args)
    {
    }

    public void Warning(string message)
    {
    }

    public void Error(string format, params object?[] args)
    {
    }

    public void Error(string message)
    {
    }

    public void Error(Exception exceptionToTrace)
    {
    }

#if NETFRAMEWORK
    public void SetupRemoteEqtTraceListeners(AppDomain? childDomain)
    {
    }
#endif
}
