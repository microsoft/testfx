// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// A specialized test execution filter for VSTest. It contains the VSTest specific properties.
/// </summary>
[Obsolete("TestCases property of this class is always null.")]
public sealed class VSTestTestExecutionFilter : ITestExecutionFilter
{
    private VSTestTestExecutionFilter()
    {
    }

    /// <summary>
    /// Gets the collection of <see cref="TestCase"/> objects.
    /// </summary>
    public ImmutableArray<TestCase>? TestCases => null;

    internal static VSTestTestExecutionFilter Instance { get; } = new();

    /// <inheritdoc />
    public bool IsAvailable => TestCases is { IsDefaultOrEmpty: false };

    /// <inheritdoc />
    public bool MatchesFilter(TestNode testNode) => TestCases?.Any(x => x.Id.ToString() == testNode.Uid.Value) is true;
}
