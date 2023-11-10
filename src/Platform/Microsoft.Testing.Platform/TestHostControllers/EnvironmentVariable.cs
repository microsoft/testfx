// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

public class EnvironmentVariable(string variable, string? value, bool isSecret, bool isLocked)
{
    public string Variable { get; } = variable;

    public string? Value { get; } = value;

    public bool IsSecret { get; } = isSecret;

    public bool IsLocked { get; } = isLocked;
}
