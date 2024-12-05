// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Json;

// This object is needed to reuse jsonite's serialization shared code.
internal sealed class JsoniteProperties : Dictionary<string, object>;
