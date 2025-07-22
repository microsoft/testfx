// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal interface ITestDataRow
{
    object? Value { get; }

    string? IgnoreMessage { get; }

    string? DisplayName { get; }

    IList<string>? TestCategories { get; }
}
