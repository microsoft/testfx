// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FSharpPlayground

open System
open System.Reflection
open Microsoft.Testing.Platform.Builder
open Microsoft.VisualStudio.TestTools.UnitTesting

module Program =
    [<EntryPoint>]
    let main args =
        // Opt-out telemetry
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1")

        task {
            let! testApplicationBuilder = TestApplication.CreateBuilderAsync(args)

            // Test MSTest
            let entryAssembly =
                match Assembly.GetEntryAssembly() with
                | null -> failwith "Entry assembly is null"
                | assembly -> assembly
            testApplicationBuilder.AddMSTest(fun () -> [| entryAssembly |]) |> ignore

            // Build and run the test application
            let! testApplication = testApplicationBuilder.BuildAsync()
            use testApp = testApplication
            let! result = testApp.RunAsync()
            return result
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously
