// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Capabilities.TestFramework;

/// <summary>
/// A capability declaring that the test framework can run a single partition of its tests.
/// This is used to support '--shard-count'.
/// </summary>
/// <remarks>
/// When <see cref="IsSupported"/> is <see langword="true"/>, the platform exposes '--shard-count &lt;count&gt;'
/// and runs each partition in a separate test host process started with '--partition &lt;index&gt;/&lt;count&gt;',
/// where 'index' is one-based and ranges from 1 through 'count'. The test framework must register and honor the '--partition' option so that
/// every test runs in exactly one partition. Test frameworks that do not provide this capability, or that report
/// <see cref="IsSupported"/> as <see langword="false"/>, do not expose '--shard-count'.
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ITestPartitioningCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Gets a value indicating whether the test framework supports running a single partition of its tests.
    /// </summary>
    bool IsSupported { get; }
}
