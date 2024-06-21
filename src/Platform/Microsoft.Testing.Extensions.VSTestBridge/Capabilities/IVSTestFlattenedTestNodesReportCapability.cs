// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Extensions.VSTestBridge.Capabilities;

/// <summary>
/// A capability to indicate whether the VSTest adapter supports flattened test nodes report.
/// This corresponds to the way Visual Studio Test Explorer displays tests.
/// </summary>
internal interface IVSTestFlattenedTestNodesReportCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Gets a value indicating whether a flag indicating whether the capability is supported.
    /// </summary>
    bool IsSupported { get; }
}
