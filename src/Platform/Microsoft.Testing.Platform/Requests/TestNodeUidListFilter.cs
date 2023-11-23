// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Requests;

public sealed class TestNodeUidListFilter(TestNodeUid[] testNodeUids) : ITestExecutionFilter
{
    public TestNodeUid[] TestNodeUids { get; } = testNodeUids;
}
