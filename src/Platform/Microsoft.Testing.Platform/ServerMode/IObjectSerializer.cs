﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode;

internal interface IObjectSerializer
{
    IDictionary<string, object?> SerializeObject(object obj);
}

internal sealed class ObjectSerializer<T>(Func<T, IDictionary<string, object?>> fn) : IObjectSerializer
{
    public IDictionary<string, object?> SerializeObject(object obj)
        => fn((T)obj);
}
