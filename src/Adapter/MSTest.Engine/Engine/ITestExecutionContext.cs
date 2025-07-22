// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Configurations;

namespace Microsoft.Testing.Framework;

public interface ITestExecutionContext
{
    CancellationToken CancellationToken { get; }

    IConfiguration Configuration { get; }

    ITestInfo TestInfo { get; }

    void CancelTestExecution();

    void CancelTestExecution(int millisecondsDelay);

    void CancelTestExecution(TimeSpan delay);

    void ReportException(Exception exception, CancellationToken? timeoutCancellationToken = null);
}
