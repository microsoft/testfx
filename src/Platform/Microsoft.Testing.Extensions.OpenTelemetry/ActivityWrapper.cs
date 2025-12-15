// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Extensions.OpenTelemetry;

internal sealed class ActivityWrapper(Activity activity) : IPlatformActivity
{
    public string? Id => activity.Id;

    public IPlatformActivity SetTag(string key, object? value)
    {
        activity.SetTag(key, value);
        return this;
    }

    public void Dispose() => activity.Dispose();
}
