// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Microsoft.Testing.Platform entry point so this F# test asset is built as an executable in v5 and can be
// hosted by vstest.console (18.10+) through the translation layer (see
// MSTest.VstestConsoleWrapper.IntegrationTests).
module Program

open System.Reflection
open Microsoft.Testing.Platform.Builder
open Microsoft.VisualStudio.TestTools.UnitTesting

[<EntryPoint>]
let main argv =
    let builder = TestApplication.CreateBuilderAsync(argv).GetAwaiter().GetResult()
    builder.AddMSTest(fun () -> Seq.singleton (Assembly.GetEntryAssembly()))
    use app = builder.BuildAsync().GetAwaiter().GetResult()
    app.RunAsync().GetAwaiter().GetResult()
