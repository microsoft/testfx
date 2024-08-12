// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Internal and self-explanatory")]
internal enum FixtureKind
{
    AssemblyInitialize,
    AssemblyCleanup,
    ClassInitialize,
    ClassCleanup,
    TestInitialize,
    TestCleanup,
}
