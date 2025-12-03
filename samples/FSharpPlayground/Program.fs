// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace FSharpPlayground

open System
open System.Reflection
open Microsoft.Testing.Platform.Builder

module Program =
    [<EntryPoint>]
    let main args =
        // Opt-out telemetry
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1")

        async {
            let! testApplicationBuilder = TestApplication.CreateBuilderAsync(args) |> Async.AwaitTask

            // Test MSTest
            testApplicationBuilder.AddMSTest(fun () -> [| Assembly.GetEntryAssembly() |]) |> ignore

            // Build and run the test application
            let! testApplication = testApplicationBuilder.BuildAsync() |> Async.AwaitTask
            use testApp = testApplication
            let! result = testApp.RunAsync() |> Async.AwaitTask
            return result
        }
        |> Async.RunSynchronously
