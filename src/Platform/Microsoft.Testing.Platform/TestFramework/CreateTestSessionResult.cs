// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TestFramework;

public sealed class CreateTestSessionResult
{
    public string? WarningMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsSuccess { get; set; }
}
