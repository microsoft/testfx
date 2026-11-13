// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp.Acceptance.IntegrationTests;

internal static class ExpressionRuleCases
{
    public static IEnumerable<(string Rule, string Source, bool ShouldReport)> Cases
    {
        get
        {
            yield return ("MSTEST0023", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: bool) = Assert.IsTrue(not value)
""", true);
            yield return ("MSTEST0023", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: bool) = Assert.IsFalse(value)
""", false);
            yield return ("MSTEST0025", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() = Assert.IsTrue(false)
""", true);
            yield return ("MSTEST0025", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: bool) = Assert.IsTrue(value)
""", false);
            yield return ("MSTEST0025", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: int) = Assert.AreNotEqual(value, value)
""", true);
            yield return ("MSTEST0032", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: int) = Assert.AreEqual(value, value)
""", true);
            yield return ("MSTEST0032", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: int) = Assert.AreEqual(42, value)
""", false);
            yield return ("MSTEST0032", """
namespace Consumer
open System.Collections.Generic
open Microsoft.VisualStudio.TestTools.UnitTesting
type NeverEqual() =
    interface IEqualityComparer<int> with
        member _.Equals(_, _) = false
        member _.GetHashCode(value) = value
type Tests() =
    member _.Check(value: int) = Assert.AreEqual(value, value, NeverEqual())
""", false);
            yield return ("MSTEST0037", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: string) = Assert.IsTrue(value.Contains("expected"))
""", true);
            yield return ("MSTEST0037", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: string) = Assert.Contains("expected", value)
""", false);
            yield return ("MSTEST0037", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: int) = Assert.IsTrue(value > 42)
""", true);
            yield return ("MSTEST0037", """
namespace Consumer
open System.Collections.Generic
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(values: List<int>) = Assert.AreEqual(0, values.Count)
""", true);
            yield return ("MSTEST0037", """
namespace Consumer
open System.Collections
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(values: Hashtable) = Assert.IsTrue(values.Contains("key"))
""", false);
            yield return ("MSTEST0038", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: int) = Assert.AreSame(42, value)
""", true);
            yield return ("MSTEST0038", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(expected: obj, actual: obj) = Assert.AreSame(expected, actual)
""", false);
            yield return ("MSTEST0046", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: string) = StringAssert.Contains(value, "expected")
""", true);
            yield return ("MSTEST0046", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(value: string) = Assert.Contains("expected", value)
""", false);
            yield return ("MSTEST0048", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<AssemblyInitialize>]
    static member Initialize(context: TestContext) = context.TestName |> ignore
""", true);
            yield return ("MSTEST0048", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check(context: TestContext) = context.TestName |> ignore
""", false);
            yield return ("MSTEST0048", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<ClassInitialize>]
    static member Initialize(context: TestContext) = context.Properties["TestName"] |> ignore
""", true);
            yield return ("MSTEST0049", """
namespace Consumer
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = task { do! Task.Delay(1) }
""", true);
            yield return ("MSTEST0049", """
namespace Consumer
open System.Threading
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = task { do! Task.Delay(1, CancellationToken.None) }
""", false);
            yield return ("MSTEST0051", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() =
        Assert.Throws<InvalidOperationException>(Action(fun () ->
            Console.WriteLine("setup")
            raise (InvalidOperationException()))) |> ignore
""", true);
            yield return ("MSTEST0051", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() =
        Assert.Throws<InvalidOperationException>(Action(fun () ->
            raise (InvalidOperationException()))) |> ignore
""", false);
            yield return ("MSTEST0054", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(context: TestContext) = context.CancellationTokenSource.Token |> ignore
""", true);
            yield return ("MSTEST0054", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(context: TestContext) = context.CancellationToken |> ignore
""", false);
            yield return ("MSTEST0058", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() =
        try raise (InvalidOperationException())
        with :? InvalidOperationException -> Assert.Fail("caught")
""", true);
            yield return ("MSTEST0058", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() =
        try raise (InvalidOperationException())
        with :? InvalidOperationException as ex when ex.Message = "unsupported" ->
            Assert.Inconclusive("unsupported")
""", false);
            yield return ("MSTEST0058", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() =
        try raise (InvalidOperationException())
        with :? InvalidOperationException -> raise (AssertFailedException("caught"))
""", true);
            yield return ("MSTEST0058", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() =
        try raise (InvalidOperationException())
        with :? InvalidOperationException as ex -> raise ex
""", false);
            yield return ("MSTEST0061", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if not (OperatingSystem.IsWindows()) then
            Assert.Inconclusive("requires Windows")
        Assert.IsTrue(true)
""", true);
            yield return ("MSTEST0061", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        Console.WriteLine("initialization")
        if not (OperatingSystem.IsWindows()) then
            Assert.Inconclusive("requires Windows")
""", false);
            yield return ("MSTEST0064", """
namespace Consumer
open System
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        Assert.Throws<InvalidOperationException>(Action(fun () ->
            Task.Delay(1).GetAwaiter().GetResult())) |> ignore
""", true);
            yield return ("MSTEST0064", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        Assert.Throws<InvalidOperationException>(Action(fun () ->
            raise (InvalidOperationException()))) |> ignore
""", false);
            yield return ("MSTEST0065", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(expected: int array, actual: int array) = Assert.AreEqual(expected, actual)
""", true);
            yield return ("MSTEST0065", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(expected: string, actual: string) = Assert.AreEqual(expected, actual)
""", false);
            yield return ("MSTEST0065", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(actual: int array) = Assert.AreEqual<int array>(null, actual)
""", false);
            yield return ("MSTEST0067", """
namespace Consumer
open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = Thread.Sleep(1)
""", true);
            yield return ("MSTEST0067", """
namespace Consumer
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = task {
        let! value = Task.FromResult(42)
        Assert.AreEqual(42, value)
    }
""", false);
            yield return ("MSTEST0067", """
namespace Consumer
open System.Threading
open Microsoft.VisualStudio.TestTools.UnitTesting
type Helper() =
    member _.Sleep() = Thread.Sleep(1)
""", false);
            yield return ("MSTEST0067", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = async { do! Async.Sleep(1) }
""", false);
            yield return ("MSTEST0068", """
namespace Consumer
open System.Collections
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(expected: int array, actual: int array) =
        CollectionAssert.AreEqual(expected :> ICollection, actual :> ICollection)
""", true);
            yield return ("MSTEST0068", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(expected: int array, actual: int array) = Assert.AreSequenceEqual(expected, actual)
""", false);
            yield return ("MSTEST0073", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; ResourceLock("shared-state")>]
type Tests() =
    [<TestMethod>]
    member _.Check() = ()
""", true);
            yield return ("MSTEST0073", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; ResourceLock(WellKnownResources.Console)>]
type Tests() =
    [<TestMethod>]
    member _.Check() = ()
""", false);
            yield return ("MSTEST0074", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = Environment.SetEnvironmentVariable("EXAMPLE", "value")
""", true);
            yield return ("MSTEST0074", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod; ResourceLock(WellKnownResources.EnvironmentVariables)>]
    member _.Check() = Environment.SetEnvironmentVariable("EXAMPLE", "value")
""", false);
            yield return ("MSTEST0074", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<AssemblyInitialize>]
    static member Initialize(context: TestContext) = Environment.SetEnvironmentVariable("EXAMPLE", "value")
""", false);
            yield return ("MSTEST0074", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestInitialize; ResourceLock(WellKnownResources.EnvironmentVariables)>]
    member _.Initialize() = Environment.SetEnvironmentVariable("EXAMPLE", "value")
""", true);
            yield return ("MSTEST0074", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass; ResourceLock(WellKnownResources.EnvironmentVariables)>]
type Tests() =
    [<TestInitialize>]
    member _.Initialize() = Environment.SetEnvironmentVariable("EXAMPLE", "value")
""", false);
            yield return ("MSTEST0075", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = Environment.CurrentDirectory <- "other"
""", true);
            yield return ("MSTEST0075", """
namespace Consumer
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod; DoNotParallelize>]
    member _.Check() = Directory.SetCurrentDirectory("other")
""", false);
            yield return ("MSTEST0075", """
namespace Consumer
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = Directory.SetCurrentDirectory("other")
""", false);
            yield return ("MSTEST0076", """
namespace Consumer
open System.Globalization
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.InvariantCulture
""", true);
            yield return ("MSTEST0076", """
namespace Consumer
open System.Globalization
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = CultureInfo.CurrentCulture <- CultureInfo.InvariantCulture
""", false);
            yield return ("MSTEST0076", """
namespace Consumer
open System.Globalization
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod; ResourceLock("culture")>]
    member _.Check() = CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.InvariantCulture
""", false);
            yield return ("MSTEST0077", """
namespace Consumer
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = File.WriteAllText("shared.txt", "content")
""", true);
            yield return ("MSTEST0077", """
namespace Consumer
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = File.ReadAllText("shared.txt") |> ignore
""", false);
            yield return ("MSTEST0077", """
namespace Consumer
open System
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = File.Copy("shared-fixture.txt", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
""", false);
            yield return ("MSTEST0079", """
namespace Consumer
open System.Runtime.InteropServices
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if RuntimeInformation.ProcessArchitecture <> Architecture.X64 then
            Assert.Inconclusive("requires X64")
        Assert.IsTrue(true)
""", true);
            yield return ("MSTEST0079", """
namespace Consumer
open System
open System.Runtime.InteropServices
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        Console.WriteLine("initialization")
        if RuntimeInformation.ProcessArchitecture <> Architecture.X64 then
            Assert.Inconclusive("requires X64")
""", false);
            yield return ("MSTEST0080", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if Environment.GetEnvironmentVariable("CI") = null then
            Assert.Inconclusive("requires CI")
        Assert.IsTrue(true)
""", true);
            yield return ("MSTEST0080", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if Environment.GetEnvironmentVariable("UNRELATED") = null then
            Assert.Inconclusive("requires unrelated setting")
        Assert.IsTrue(true)
""", false);
            yield return ("MSTEST0083", """
namespace Consumer
open System.Diagnostics
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if not (File.Exists("tool.exe")) then
            Assert.Inconclusive("requires tool")
        Process.Start("tool.exe") |> ignore
""", true);
            yield return ("MSTEST0083", """
namespace Consumer
open System.Diagnostics
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if not (File.Exists("tool.exe")) then
            Assert.Inconclusive("requires tool")
        Process.Start("other.exe") |> ignore
""", false);
            yield return ("MSTEST0023", """
namespace Consumer
type Assert =
    static member IsTrue(condition: bool) = ()
type Tests() =
    member _.Check(value: bool) = Assert.IsTrue(not value)
""", false);
            yield return ("MSTEST0032", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<Sealed>]
type NeverEqual() =
    interface IEquatable<NeverEqual> with
        member _.Equals(_) = false
type Tests() =
    member _.Check(value: NeverEqual) = Assert.AreEqual(value, value)
""", false);
            yield return ("MSTEST0037", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check(expected: int array, actual: int array) = Assert.IsTrue((expected = actual))
""", false);
            yield return ("MSTEST0064", """
namespace Consumer
open System
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        Assert.Throws<InvalidOperationException>(Action(fun () ->
            ValueTask.CompletedTask.GetAwaiter().GetResult())) |> ignore
""", false);
            yield return ("MSTEST0074", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: DoNotParallelize>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = Console.SetOut(System.IO.TextWriter.Null)
""", false);
            yield return ("MSTEST0074", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize(Scope = ExecutionScope.MethodLevel)>]
do ()
type Environment =
    static member SetEnvironmentVariable(name: string, value: string) = ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() = Environment.SetEnvironmentVariable("EXAMPLE", "value")
""", false);
            yield return ("MSTEST0079", """
namespace Consumer
open System.Runtime.InteropServices
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; CICondition(ConditionMode.Include)>]
    member _.Check() =
        if RuntimeInformation.ProcessArchitecture <> Architecture.X64 then
            Assert.Inconclusive("requires X64")
        Assert.IsTrue(true)
""", false);
            yield return ("MSTEST0080", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if Environment.GetEnvironmentVariable("CI") = "false" || Environment.GetEnvironmentVariable("OTHER") = null then
            Assert.Inconclusive("unrelated condition")
        Assert.IsTrue(true)
""", false);
            yield return ("MSTEST0083", """
namespace Consumer
open System.Diagnostics
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if File.Exists("tool.exe") <> false then
            Assert.Inconclusive("tool present")
        Process.Start("tool.exe") |> ignore
""", false);
            yield return ("MSTEST0083", """
namespace Consumer
open System.Diagnostics
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if not (File.Exists("tool.exe")) then
            Assert.Inconclusive("requires tool")
        let startLater = fun () -> Process.Start("tool.exe") |> ignore
        ()
""", false);
            yield return ("MSTEST0083", """
namespace Consumer
open System.Diagnostics
open System.IO
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Check() =
        if not (File.Exists("tool.exe")) then
            Assert.Inconclusive("requires tool")
        Process.Start(ProcessStartInfo("tool.exe", FileName = "other.exe")) |> ignore
""", false);
            yield return ("MSTEST0025", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() = Assert.IsFalse(true)
""", true);
            yield return ("MSTEST0032", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    member _.Check() = Assert.IsTrue(true)
""", true);
            yield return ("MSTEST0073", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
module Resources =
    [<Literal>]
    let Shared = "shared-state"
[<TestClass; ResourceLock(Resources.Shared)>]
type Tests() =
    [<TestMethod>]
    member _.Check() = ()
""", false);
            yield return ("MSTEST0073", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; ResourceLock("method-state")>]
    member _.Check() = ()
""", true);
            yield return ("MSTEST0073", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
type ResourceLockAttribute(resource: string) =
    inherit Attribute()
[<TestClass; ResourceLock("unrelated")>]
type Tests() =
    [<TestMethod>]
    member _.Check() = ()
""", false);
        }
    }
}
