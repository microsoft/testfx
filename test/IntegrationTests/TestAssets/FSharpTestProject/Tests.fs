namespace FSharpTestProject

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type UnitTest1 () =

    [<TestMethod>]
    member this.``Test method passing with a . in it`` () =
        Assert.IsTrue(true);
