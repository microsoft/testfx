// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

internal interface IPlatformTestExecutionActivity : IPlatformActivity
{
    IDisposable Enter();

    void Stop(DateTimeOffset endTime);
}
