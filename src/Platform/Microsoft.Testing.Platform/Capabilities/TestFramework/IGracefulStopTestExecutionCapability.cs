// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// A capability to support stopping test execution gracefully, without cancelling/aborting everything.
/// This is used to support '--maximum-failed-tests'.
/// </summary>
/// <remarks>
/// Test frameworks can choose to run any needed cleanup when cancellation is requested.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IGracefulStopTestExecutionCapability : ITestFrameworkCapability
{
    Task StopTestExecutionAsync(CancellationToken cancellationToken);
}
