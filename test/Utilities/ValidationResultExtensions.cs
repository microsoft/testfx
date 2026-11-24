// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Testing.TestInfrastructure;

internal static class ValidationResultExtensions
{
    public static void AssertIsValid(this ValidationResult result, string? message = null)
    {
        Assert.IsTrue(result.IsValid, message);
        Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
    }
}
