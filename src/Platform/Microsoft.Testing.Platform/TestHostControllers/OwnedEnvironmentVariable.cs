// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

public record OwnedEnvironmentVariable(IExtension Owner, string Variable, string? Value, bool IsSecret, bool IsLocked)
    : EnvironmentVariable(Variable, Value, IsSecret, IsLocked);
