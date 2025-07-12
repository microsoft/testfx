// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Tools;

internal interface ITool : IExtension
{
    string Name { get; }

    Task<int> RunAsync(CancellationToken cancellationToken);
}
