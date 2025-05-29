// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.Capabilities;

/// <summary>
/// A capability interface to control the <see cref="TestNodeUid"/> created by the bridge.
/// </summary>
public interface ITestNodeUidProviderCapability : ITestFrameworkCapability
{
    /// <summary>
    /// Gets the unique identifier (TestNodeUid) for the given VSTest <see cref="TestCase"/>.
    /// </summary>
    /// <param name="testCase">The VSTest test case.</param>
    /// <returns>The test node uid to be used when creating MTP's <see cref="TestNode"/>.</returns>
    string GetTestNodeUid(TestCase testCase);

    /// <summary>
    /// The framework should be capable of filtering tests based on the <see cref="TestNodeUid"/>.
    /// The bridge converts a <see cref="TestNodeUidListFilter"/> to a VSTest filter expression.
    /// </summary>
    /// <returns>The property name should the bridge use to filter tests based on the <see cref="TestNodeUid"/>.</returns>
    string GetFilterPropertyName();
}
