// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Helpers;

internal enum FixtureParameterMode
{
    /// <summary>
    /// Indicates that there must not be a TestContext parameter.
    /// </summary>
    MustNotHaveTestContext,

    /// <summary>
    /// Indicates that a TestContext parameter is mandatory.
    /// </summary>
    MustHaveTestContext,

    /// <summary>
    /// Indicates that a TestContext parameter is optional.
    /// </summary>
    OptionalTestContext,
}
