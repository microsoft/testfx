namespace FSharpTestProject

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type ``This is a test type`` () =

    [<TestMethod>]
    member this.``Test method passing with a . in it`` () =
        Assert.IsTrue(true);