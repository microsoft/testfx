// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace MSTest.Analyzers;

[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1602:Enumeration items should be documented", Justification = "Internal enum self explanatory")]
internal enum FixtureMethodSignatureChanges
{
    None = 0,
    MakeStatic = 1,
    RemoveStatic = 2,
    MakePublic = 4,
    AddTestContextParameter = 8,
    RemoveParameters = 16,
    FixAsyncVoid = 32,
    FixReturnType = 64,
    RemoveGeneric = 128,
}
