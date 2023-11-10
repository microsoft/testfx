// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

public sealed class OwnedEnvironmentVariable(IExtension owner, string variable, string? value, bool isSecret, bool isLocked)
    : EnvironmentVariable(variable, value, isSecret, isLocked)
{
    public IExtension Owner { get; } = owner;
}
