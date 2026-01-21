// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.VSTestAdapter;

internal sealed class EqtTraceLogger : MarshalByRefObject, ITraceLogger
{
    private EqtTraceLogger()
    {
    }

    public static ITraceLogger Instance { get; } = new EqtTraceLogger();

    public bool IsVerboseEnabled => EqtTrace.IsVerboseEnabled;

    public bool IsInfoEnabled => EqtTrace.IsInfoEnabled;

    public bool IsWarningEnabled => EqtTrace.IsWarningEnabled;

    public bool IsErrorEnabled => EqtTrace.IsErrorEnabled;

    public void Verbose(string format, params object?[] args)
    => EqtTrace.Verbose(format, args);

    public void Verbose(string message)
        => EqtTrace.Verbose(message);

    public void Info(string format, params object?[] args)
        => EqtTrace.Info(format, args);

    public void Info(string message)
        => EqtTrace.Info(message);

    public void Warning(string format, params object?[] args)
    => EqtTrace.Warning(format, args);

    public void Warning(string message)
        => EqtTrace.Warning(message);

    public void Error(string format, params object?[] args)
    => EqtTrace.Error(format, args);

    public void Error(string message)
        => EqtTrace.Error(message);

    public void Error(Exception exceptionToTrace)
        => EqtTrace.Error(exceptionToTrace);

#if NETFRAMEWORK
    public void SetupRemoteEqtTraceListeners(AppDomain? childDomain)
        => EqtTrace.SetupRemoteEqtTraceListeners(childDomain);
#endif
}
