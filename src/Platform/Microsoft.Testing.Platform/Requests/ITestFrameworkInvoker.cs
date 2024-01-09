// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

internal interface ITestFrameworkInvoker : IExtension
{
    Task ExecuteAsync(ITestFramework testFramework, ClientInfo client, CancellationToken cancellationToken);
}
