// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

namespace MSTest.Acceptance.IntegrationTests.Messages.V100;

public class TelemetryCollector : ConcurrentBag<TelemetryPayload>;
