// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Requests;

public sealed class AggregateFilter(params IReadOnlyList<ITestExecutionFilter> innerFilters) : ITestExecutionFilter
{
    public IReadOnlyList<ITestExecutionFilter> InnerFilters { get; } = innerFilters;

    public bool IsAvailable => true;
}
