// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal interface IPlatformActivity : IDisposable
{
    string? Id { get; }

    IPlatformActivity SetTag(string key, object? value);
}
