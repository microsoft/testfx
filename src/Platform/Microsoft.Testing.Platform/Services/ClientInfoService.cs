// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal sealed record ClientInfoService(string Id, string Version) : IClientInfo;
