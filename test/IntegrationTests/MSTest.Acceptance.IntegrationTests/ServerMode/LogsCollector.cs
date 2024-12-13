// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using static Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100.TestingPlatformClient;

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

// Not using ConcurrentBag because it's unordered.
public class LogsCollector : BlockingCollection<Log>;
