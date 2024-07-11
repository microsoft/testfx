// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Messages;

internal static class TestNodePropertiesCategories
{
    public static Type[] WellKnownTestNodeDiscoveredProperties { get; } =
        [
            typeof(DiscoveredTestNodeStateProperty),
        ];

    public static Type[] WellKnownTestNodeTestRunOutcomeProperties { get; } =
        [
            typeof(PassedTestNodeStateProperty),

            typeof(FailedTestNodeStateProperty),
            typeof(ErrorTestNodeStateProperty),
            typeof(TimeoutTestNodeStateProperty),
            typeof(CancelledTestNodeStateProperty),
        ];

    public static Type[] WellKnownTestNodeTestRunOutcomeFailedProperties { get; } =
        [
            typeof(FailedTestNodeStateProperty),
            typeof(ErrorTestNodeStateProperty),
            typeof(TimeoutTestNodeStateProperty),
            typeof(CancelledTestNodeStateProperty),
        ];
}
