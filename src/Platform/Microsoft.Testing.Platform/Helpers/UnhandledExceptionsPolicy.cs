// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

internal sealed class UnhandledExceptionsPolicy : IUnhandledExceptionsPolicy
{
    public UnhandledExceptionsPolicy(bool fastFailOnFailure)
    {
        FastFailOnFailure = fastFailOnFailure;
    }

    public bool FastFailOnFailure { get; }
}
