// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal record struct TestNodeStatistics(long TotalDiscoveredTests, long TotalPassedTests, long TotalFailedTests, long TotalPassedRetries, long TotalFailedRetries);
