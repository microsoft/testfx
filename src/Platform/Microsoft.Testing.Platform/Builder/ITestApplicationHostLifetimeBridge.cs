// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Builder;

internal interface ITestApplicationHostLifetimeBridge
{
    void Connect(Action requestTestApplicationStop, CancellationToken testApplicationStopping);

    void Disconnect();
}
