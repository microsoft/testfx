// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class SystemSemaphoreFactory : ISemaphoreFactory
{
    public ISemaphore Create(int initial, int maximum)
        => new SystemSemaphore(initial, maximum);
}
