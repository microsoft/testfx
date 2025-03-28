// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Used for extensions or filters to ask for the test nodes from the test framework.
/// An example is a Batch filter that needs to compare all of the test nodes to know whether to filter or not.
/// Or an extension that might add some useful Test Node properties to all tests.
/// </summary>
public interface IReceiveTestNodesExtension
{
    /// <summary>
    /// Receives a list of test nodes.
    /// </summary>
    /// <param name="testNodes">A read-only list of test nodes.</param>
    void ReceiveTestNodes(IReadOnlyList<TestNode> testNodes);
}
