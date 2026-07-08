// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// Identifies the lifecycle scope of a fixture method, controlling which validation is applied when
/// registering a fixture-method analyzer through <see cref="FixtureMethodAnalyzerHelper.RegisterFixtureAnalyzer"/>.
/// </summary>
internal enum FixtureKind
{
    /// <summary>
    /// An instance fixture method, e.g. <c>[TestInitialize]</c> or <c>[TestCleanup]</c>.
    /// </summary>
    Instance,

    /// <summary>
    /// A class fixture method, e.g. <c>[ClassInitialize]</c> or <c>[ClassCleanup]</c>.
    /// </summary>
    Class,

    /// <summary>
    /// An assembly fixture method, e.g. <c>[AssemblyInitialize]</c> or <c>[AssemblyCleanup]</c>.
    /// </summary>
    Assembly,
}
