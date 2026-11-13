// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp.Acceptance.IntegrationTests;

internal static class DataRuleCases
{
    public static IEnumerable<(string Rule, string Source, bool ShouldReport)> Cases =>
    [
        ("MSTEST0078", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Base() =
    [<TestMethod; DependsOn(typeof<Tests>, "Run")>]
    member _.Run() = ()
and [<TestClass>] Tests() =
    inherit Base()
""", true),
        ("MSTEST0078", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DependsOn("Second")>]
    abstract First: unit -> unit
    default _.First() = ()
    [<TestMethod; DependsOn("First")>]
    member _.Second() = ()
""", true),
        ("MSTEST0014", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(1, 2)>]
    member _.Run(value: int) = ()
""", true),
        ("MSTEST0014", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(1)>]
    member _.Run(value: int) = ()
""", false),
        ("MSTEST0018", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DynamicData("Missing")>]
    member _.Run(value: int) = ()
""", true),
        ("MSTEST0018", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    static member Data = [| [| box 1 |] |]
    [<TestMethod; DynamicData("Data")>]
    member _.Run(value: int) = ()
""", false),
        ("MSTEST0042", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(1); DataRow(1)>]
    member _.Run(value: int) = ()
""", true),
        ("MSTEST0042", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(1); DataRow(2)>]
    member _.Run(value: int) = ()
""", false),
        ("MSTEST0052", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    static member Data = [| [| box 1 |] |]
    [<TestMethod; DynamicData("Data", DynamicDataSourceType.Property)>]
    member _.Run(value: int) = ()
""", true),
        ("MSTEST0052", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    static member Data = [| [| box 1 |] |]
    [<TestMethod; DynamicData("Data")>]
    member _.Run(value: int) = ()
""", false),
        ("MSTEST0070", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Conditions =
    static member Ready = 42
[<TestClass; MemberCondition(typeof<Conditions>, "Ready")>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""", true),
        ("MSTEST0070", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Conditions =
    static member Ready = true
[<TestClass; MemberCondition(typeof<Conditions>, "Ready")>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""", false),
        ("MSTEST0072", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: AssemblyFixtureProvider(typeof<obj>)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""", true),
        ("MSTEST0072", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""", false),
        ("MSTEST0078", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DependsOn("Missing")>]
    member _.Run() = ()
""", true),
        ("MSTEST0078", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.First() = ()
    [<TestMethod; DependsOn("First")>]
    member _.Run() = ()
""", false),
        ("MSTEST0078", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DependsOn("Second")>]
    member _.First() = ()
    [<TestMethod; DependsOn("First")>]
    member _.Second() = ()
""", true),
        ("MSTEST0081", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: TestFilterProvider(typeof<string>)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""", true),
        ("MSTEST0081", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() = ()
""", false),
        ("MSTEST0084", """
namespace Consumer
open System.Runtime.Versioning
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; SupportedOSPlatform("windows")>]
    member _.Run() = ()
""", true),
        ("MSTEST0084", """
namespace Consumer
open System.Runtime.Versioning
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; SupportedOSPlatform("windows"); OSCondition(OperatingSystems.Windows)>]
    member _.Run() = ()
""", false),
        ("MSTEST0087", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(1, DisplayName = "case"); DataRow(2, DisplayName = "case")>]
    member _.Run(value: int) = ()
""", true),
        ("MSTEST0087", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(1, DisplayName = "first"); DataRow(2, DisplayName = "second")>]
    member _.Run(value: int) = ()
""", false),
        ("MSTEST0089", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() =
        let row = DataRowAttribute(1, DisplayName = "full", ArgumentsDisplayName = "args")
        ignore row
""", true),
        ("MSTEST0089", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Run() =
        let row = DataRowAttribute(1, ArgumentsDisplayName = "args")
        ignore row
""", false),
    ];
}
