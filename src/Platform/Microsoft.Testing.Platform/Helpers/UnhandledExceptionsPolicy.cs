﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class UnhandledExceptionsPolicy(bool fastFailOnFailure) : IUnhandledExceptionsPolicy
{
    public bool FastFailOnFailure { get; } = fastFailOnFailure;
}
