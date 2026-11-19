// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Messages;

/// <summary>
/// Represents the test outcome and its retry execution statistics.
/// </summary>
/// <param name="HasPassed"> The test's single outcome or its final outcome. </param>
/// <param name="TotalPassedRetries"> The number of times the adapter reported a Passed outcome when retrying. </param>
/// <param name="TotalFailedRetries"> The number of times the adapter reported a Failed outcome when retrying. </param>
internal record struct TestNodeStateStatistics(bool HasPassed, long TotalPassedRetries, long TotalFailedRetries);
