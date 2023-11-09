// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class InformativeCommandLineTestHost : ITestHost
{
    private readonly int _returnValue;

    public InformativeCommandLineTestHost(int returnValue)
    {
        _returnValue = returnValue;
    }

    public Task<int> RunAsync() => Task.FromResult(_returnValue);
}
