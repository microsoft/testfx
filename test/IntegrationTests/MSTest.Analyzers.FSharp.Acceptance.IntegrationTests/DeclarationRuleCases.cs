// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTest.Analyzers.FSharp.Acceptance.IntegrationTests;

internal static class DeclarationRuleCases
{
    public static IEnumerable<(string Rule, string Source, bool ShouldReport)> Cases
    {
        get
        {
            // The driver supplies IsMSTestTestAdapterReferenced=true for MSTEST0001.
            yield return ("MSTEST0001", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0001", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0001", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: DoNotParallelize>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0002", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type internal Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0002", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: DiscoverInternals>]
do ()
[<TestClass>]
type internal Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0004", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Helper() =
    member _.Value = 42
""", true);
            yield return ("MSTEST0004", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type internal Helper() =
    member _.Value = 42
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0005", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    member _.TestContext : TestContext = Unchecked.defaultof<TestContext>
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0005", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    member val TestContext : TestContext = Unchecked.defaultof<TestContext> with get, set
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0005", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(context: TestContext) =
    member _.TestContext = context
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0005", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(context: TestContext) =
    member _.testContext = context
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0005", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(context: TestContext) =
    let other: TestContext = Unchecked.defaultof<TestContext>
    member _.TestContext = other
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0005", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    static member val TestContext : TestContext = Unchecked.defaultof<TestContext> with get, set
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0007", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<Owner("owner"); Priority(1); Description("description"); WorkItem(42)>]
    member _.Helper() = ()
""", true);
            yield return ("MSTEST0007", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; Owner("owner"); Priority(1); Description("description"); WorkItem(42)>]
    member _.Test() = ()
""", false);

            foreach ((string rule, string attribute) in new[]
            {
                ("MSTEST0008", "TestInitialize"),
                ("MSTEST0009", "TestCleanup"),
            })
            {
                yield return (rule, $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<{{attribute}}>]
    static member Fixture() = ()
""", true);
                yield return (rule, $$"""
namespace Consumer
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<{{attribute}}>]
    member _.Fixture() : Task = Task.CompletedTask
""", false);
                yield return (rule, $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; AbstractClass>]
type Tests() =
    [<{{attribute}}>]
    abstract Fixture: unit -> unit
    default _.Fixture() = ()
""", false);
                yield return (rule, $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; AbstractClass>]
type Tests() =
    [<{{attribute}}>]
    abstract Fixture: unit -> unit
""", true);
                yield return (rule, $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<{{attribute}}>]
    member internal _.Fixture() = ()
""", true);
            }

            foreach ((string rule, string attribute, bool contextRequired) in new[]
            {
                ("MSTEST0010", "ClassInitialize", true),
                ("MSTEST0011", "ClassCleanup", false),
                ("MSTEST0012", "AssemblyInitialize", true),
                ("MSTEST0013", "AssemblyCleanup", false),
            })
            {
                yield return (rule, $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<{{attribute}}>]
    member _.Fixture() = ()
""", true);
                yield return (rule, $$"""
namespace Consumer
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<{{attribute}}>]
    static member Fixture(context: TestContext) : Task = Task.CompletedTask
""", false);
                yield return (rule, $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<{{attribute}}>]
    static member Fixture(context: int) = ()
""", true);
                yield return (rule, $$"""
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<{{attribute}}>]
    static member Fixture() = ()
""", contextRequired);
            }

            yield return ("MSTEST0010", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; AbstractClass>]
type BaseTests<'T>() =
    [<ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)>]
    static member Initialize(context: TestContext) = ()
""", false);
            yield return ("MSTEST0011", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed>]
type Tests() =
    [<ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)>]
    static member Cleanup() = ()
""", true);
            yield return ("MSTEST0015", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; Ignore("Tracked maintenance")>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0015", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Ignore("Tracked maintenance")>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0016", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    member _.Helper() = 42
""", true);
            yield return ("MSTEST0016", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<AbstractClass>]
type BaseTests() =
    [<TestMethod>]
    member _.InheritedTest() = ()
[<TestClass>]
type Tests() =
    inherit BaseTests()
""", false);
            yield return ("MSTEST0019", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    do System.GC.KeepAlive("initialize")
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0019", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(context: TestContext) =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0020", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestInitialize>]
    member _.Initialize() = ()
""", true);
            yield return ("MSTEST0020", """
namespace Consumer
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestInitialize>]
    member _.Initialize() : Task = Task.CompletedTask
""", false);
            yield return ("MSTEST0021", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestCleanup>]
    member _.Cleanup() = ()
""", true);
            yield return ("MSTEST0021", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    interface IDisposable with
        member _.Dispose() = ()
""", false);
            yield return ("MSTEST0022", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    interface IDisposable with
        member _.Dispose() = ()
""", true);
            yield return ("MSTEST0022", """
namespace Consumer
open System
open System.Threading.Tasks
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    interface IAsyncDisposable with
        member _.DisposeAsync() = ValueTask()
""", true);
            yield return ("MSTEST0022", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    member _.Dispose() = ()
""", false);
            yield return ("MSTEST0024", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    static member val Stored : TestContext = Unchecked.defaultof<TestContext> with get, set
    [<ClassInitialize>]
    static member Initialize(context: TestContext) =
        Tests.Stored <- context
""", true);
            yield return ("MSTEST0024", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    member val Stored : TestContext = Unchecked.defaultof<TestContext> with get, set
    [<TestMethod>]
    member this.Test(context: TestContext) =
        this.Stored <- context
""", false);
            yield return ("MSTEST0029", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    member _.ForgottenTest() = ()
""", true);
            yield return ("MSTEST0029", """
namespace Consumer
open System
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    member private _.Helper() = ()
    [<TestMethod>]
    member _.Test() = ()
    [<TestInitialize>]
    member _.Initialize() = ()
    interface IDisposable with
        member _.Dispose() = ()
""", false);
            yield return ("MSTEST0030", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0030", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<AbstractClass>]
type BaseTests() =
    [<TestMethod>]
    member _.InheritedTest() = ()
[<TestClass>]
type Tests() =
    inherit BaseTests()
""", false);
            yield return ("MSTEST0031", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; System.ComponentModel.Description("Description")>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0031", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; Description("Description")>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0035", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Helpers() =
    [<DeploymentItem("input.txt")>]
    member _.Helper() = ()
""", true);
            yield return ("MSTEST0035", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<AbstractClass; DeploymentItem("input.txt")>]
type BaseTests() =
    [<TestMethod; DeploymentItem("input.txt")>]
    member _.Test() = ()
[<TestClass; DeploymentItem("input.txt")>]
type Tests() =
    inherit BaseTests()
""", false);
            yield return ("MSTEST0036", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type BaseTests() =
    member _.Helper(value: int) = ()
[<TestClass>]
type Tests() =
    inherit BaseTests()
    member _.Helper(value: int) = ()
""", true);
            yield return ("MSTEST0036", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type BaseTests() =
    member _.Helper(value: int) = ()
[<TestClass>]
type Tests() =
    inherit BaseTests()
    member _.Helper(value: string) = ()
""", false);
            yield return ("MSTEST0036", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<AbstractClass>]
type BaseTests() =
    abstract Helper: unit -> unit
    default _.Helper() = ()
[<TestClass>]
type Tests() =
    inherit BaseTests()
    override _.Helper() = ()
""", false);
            yield return ("MSTEST0041", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<OSCondition(OperatingSystems.Windows)>]
type Helper() = class end
""", true);
            yield return ("MSTEST0041", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; OSCondition(OperatingSystems.Windows)>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0043", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type Helper() =
    [<Retry(2)>]
    member _.Helper() = ()
""", true);
            yield return ("MSTEST0043", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Retry(2)>]
type Tests() =
    [<TestMethod; Retry(2)>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0044", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<DataTestMethod; DataRow(42)>]
    member _.Test(value: int) = ()
""", true);
            yield return ("MSTEST0044", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type CustomAttribute() =
    inherit DataTestMethodAttribute()
""", true);
            yield return ("MSTEST0044", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(42)>]
    member _.Test(value: int) = ()
""", false);
            yield return ("MSTEST0045", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; Timeout(1000)>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0045", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; Timeout(1000, CooperativeCancellation = true)>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0050", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<GlobalTestInitialize>]
    member _.Initialize(context: TestContext) = ()
""", true);
            yield return ("MSTEST0050", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<GlobalTestInitialize>]
    static member Initialize(context: TestContext) = ()
    [<GlobalTestCleanup>]
    static member Cleanup(context: TestContext) = ()
""", false);
            yield return ("MSTEST0050", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: DiscoverInternals>]
do ()
[<TestClass>]
type internal Tests() =
    [<GlobalTestCleanup>]
    static member Cleanup(context: TestContext) = ()
""", true);
            yield return ("MSTEST0056", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod("A display name")>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0056", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod(DisplayName = "A display name")>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0056", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
module AttributeFactory =
    let create () = TestMethodAttribute("A display name")
""", true);
            yield return ("MSTEST0056", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
module AttributeFactory =
    let create (original: TestMethodAttribute) =
        TestMethodAttribute(original.DeclaringFilePath)
""", false);
            yield return ("MSTEST0056", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
module AttributeFactory =
    let create () = TestMethodAttribute()
""", false);
            yield return ("MSTEST0056", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
module AttributeFactory =
    let create (displayName: string) = TestMethodAttribute(displayName)
""", true);
            yield return ("MSTEST0057", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type CustomAttribute() =
    inherit TestMethodAttribute()
""", true);
            yield return ("MSTEST0057", """
namespace Consumer
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Microsoft.VisualStudio.TestTools.UnitTesting
type CustomAttribute
    ([<CallerFilePath; Optional; DefaultParameterValue("")>] filePath: string,
     [<CallerLineNumber; Optional; DefaultParameterValue(-1)>] lineNumber: int) =
    inherit TestMethodAttribute(filePath, lineNumber)
""", false);
            yield return ("MSTEST0057", """
namespace Consumer
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Microsoft.VisualStudio.TestTools.UnitTesting
type CustomAttribute
    ([<CallerFilePath; Optional; DefaultParameterValue("")>] filePath: string,
     [<Optional; DefaultParameterValue(-1)>] lineNumber: int) =
    inherit TestMethodAttribute(filePath, lineNumber)
""", true);
            yield return ("MSTEST0059", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize>]
[<assembly: DoNotParallelize>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0059", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: Parallelize>]
do ()
[<TestClass; DoNotParallelize>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0060", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type CustomAttribute() =
    inherit TestMethodAttribute()
[<TestClass>]
type Tests() =
    [<TestMethod; Custom>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0060", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
type CustomAttribute() =
    inherit TestMethodAttribute()
[<TestClass>]
type Tests() =
    [<Custom>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0062", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test(value: byref<int>) = value <- 42
""", true);
            yield return ("MSTEST0062", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod; DataRow(42)>]
    member _.Test(value: int) = ()
""", false);
            yield return ("MSTEST0063", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(value: string) =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0063", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(context: TestContext) =
    [<TestMethod>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0063", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests private () =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0071", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod(DisplayName = "Test")>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0071", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod(DisplayName = "An informative display name")>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0085", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; AbstractClass>]
type BaseTests() =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0085", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<AbstractClass>]
type BaseTests() =
    [<TestMethod>]
    member _.Test() = ()
[<TestClass>]
type Tests() =
    inherit BaseTests()
""", false);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; DoNotParallelize>]
type Tests() =
    [<TestMethod; DoNotParallelize>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; TestCategory("class")>]
type Tests() =
    [<TestMethod; TestCategory("method")>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; OSCondition(OperatingSystems.Windows)>]
type Tests() =
    [<TestMethod; OSCondition(OperatingSystems.Windows ||| OperatingSystems.Linux)>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; OSCondition(OperatingSystems.Windows)>]
type Tests() =
    [<TestMethod; OSCondition(OperatingSystems.Windows)>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; ResourceLock("resource")>]
type Tests() =
    [<TestMethod; ResourceLock("resource", Mode = ResourceAccessMode.Read)>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; Retry(2, MillisecondsDelayBetweenRetries = 10)>]
type Tests() =
    [<TestMethod; Retry(2, MillisecondsDelayBetweenRetries = 20)>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestCategory("shared"); TestProperty("key", "value"); DeploymentItem("Input\\")>]
type BaseTests() = class end
[<TestClass>]
type Tests() =
    inherit BaseTests()
    [<TestMethod; TestCategory("shared"); TestProperty("key", "value"); DeploymentItem("input")>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; Ignore("class reason"); Retry(2)>]
type Tests() =
    [<TestMethod; Ignore("method reason"); Retry(2)>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; CICondition(ConditionMode.Include)>]
type Tests() =
    [<TestMethod; CICondition(ConditionMode.Include)>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; ArchitectureCondition(TestArchitectures.X64)>]
type Tests() =
    [<TestMethod; ArchitectureCondition(TestArchitectures.X64 ||| TestArchitectures.Arm64)>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; DependsOn("First")>]
type Tests() =
    [<TestMethod>]
    member _.First() = ()
    [<TestMethod; DependsOn("First")>]
    member _.Second() = ()
""", true);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; DependsOn("First")>]
type Tests() =
    [<TestMethod; DependsOn("First")>]
    member _.First() = ()
""", false);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; Sealed; OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)>]
type Tests() =
    [<TestMethod; OSCondition(OperatingSystems.Linux ||| OperatingSystems.OSX ||| OperatingSystems.FreeBSD)>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0086", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass; ResourceLock("resource", Mode = ResourceAccessMode.Read)>]
type Tests() =
    [<TestMethod; ResourceLock("resource")>]
    member _.Test() = ()
""", false);
            yield return ("MSTEST0062", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test(value: inref<int>) = System.GC.KeepAlive(value)
""", false);
            // The driver supplies MSTestHostTestClassInjectionEnabled=true for MSTEST0088.
            yield return ("MSTEST0088", """
namespace Consumer
open System.Reflection
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: AssemblyMetadata("MSTestHostTestClassInjectionUnsupportedMode", "PublishAot")>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", true);
            yield return ("MSTEST0088", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false);
        }
    }

    public static IEnumerable<(string Rule, string Source, bool ShouldReport, IReadOnlyDictionary<string, string> Settings)> ConfiguredCases
    {
        get
        {
            yield return ("MSTEST0001", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false, new Dictionary<string, string> { ["IsMSTestTestAdapterReferenced"] = "false" });
            yield return ("MSTEST0063", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests(service: string) =
    [<TestMethod>]
    member _.Test() = ()
""", false, new Dictionary<string, string> { ["MSTestHostTestClassInjectionEnabled"] = "true" });
            yield return ("MSTEST0088", """
namespace Consumer
open System.Reflection
open Microsoft.VisualStudio.TestTools.UnitTesting
[<assembly: AssemblyMetadata("MSTestHostTestClassInjectionUnsupportedMode", "PublishAot")>]
do ()
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false, new Dictionary<string, string> { ["MSTestHostTestClassInjectionEnabled"] = "false" });
            yield return ("MSTEST0088", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", true, new Dictionary<string, string>
            {
                ["MSTestHostTestClassInjectionEnabled"] = "true",
                ["PublishAot"] = "true",
            });
            yield return ("MSTEST0088", """
namespace Consumer
open Microsoft.VisualStudio.TestTools.UnitTesting
[<TestClass>]
type Tests() =
    [<TestMethod>]
    member _.Test() = ()
""", false, new Dictionary<string, string>
            {
                ["MSTestHostTestClassInjectionEnabled"] = "true",
                ["PublishTrimmed"] = "true",
            });
        }
    }
}
