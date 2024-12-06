// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal interface IPoliciesService
{
    void RegisterOnMaxFailedTestsCallback(Func<CancellationToken, Task> callback);

    void RegisterOnAbortCallback(Func<Task> callback);
}
