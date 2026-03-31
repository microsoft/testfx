// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHost;

namespace Microsoft.Testing.Platform.ServerMode;

internal interface IPushOnlyProtocolConsumer : IDataConsumer, ITestSessionLifetimeHandler;
