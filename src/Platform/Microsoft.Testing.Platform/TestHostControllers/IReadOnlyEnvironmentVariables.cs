// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

public interface IReadOnlyEnvironmentVariables
{
    bool TryGetVariable(string variable, [NotNullWhen(true)] out OwnedEnvironmentVariable? environmentVariable);
}
