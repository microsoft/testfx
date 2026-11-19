// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

internal interface ITraceLogger
{
    bool IsVerboseEnabled { get; }

    bool IsInfoEnabled { get; }

    bool IsWarningEnabled { get; }

    bool IsErrorEnabled { get; }

    void Verbose(string format, params object?[] args);

    void Verbose(string message);

    void Info(string format, params object?[] args);

    void Info(string message);

    void Warning(string format, params object?[] args);

    void Warning(string message);

    void Error(string format, params object?[] args);

    void Error(string message);

    void Error(Exception exceptionToTrace);

#if NETFRAMEWORK
    void SetupRemoteEqtTraceListeners(AppDomain? childDomain);
#endif
}
