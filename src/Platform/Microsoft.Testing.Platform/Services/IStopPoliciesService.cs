﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal interface IStopPoliciesService
{
    bool IsMaxFailedTestsTriggered { get; }

    bool IsAbortTriggered { get; }

    void RegisterOnMaxFailedTestsCallback(Func<CancellationToken, Task> callback);

    void RegisterOnAbortCallback(Func<Task> callback);

    Task ExecuteMaxFailedTestsCallbacksAsync(CancellationToken cancellationToken);

    Task ExecuteAbortCallbacksAsync();
}
