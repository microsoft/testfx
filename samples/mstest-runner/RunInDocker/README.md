# Run tests in docker

This example shows one way to test a dockerized application. The application is built, and depends on the ASP.NET runtime image `mcr.microsoft.com/dotnet/aspnet:8.0`. This image .NET runtime, but does not contain the full .NET SDK.

A later stage in the Dockerfile, then adds a layer containing the tests for the application. The tests start the server application and execute integration tests against it. Again there is no .NET SDK needed to run those tests, just the .NET runtime that is coming from the `mcr.microsoft.com/dotnet/aspnet:8.0` image.

The advantage here is that we can build our application, and test the same build of the application that we are shipping. Our images are also smaller, becuase they don't depend on .NET SDK.

## Usage

Build the final application, and tag it:

```cli
RunInDocker> docker build . --target=final -t my-server
```

Build the tests, most of the steps are already done, and are picked up from cache, including the application ('final' stage) we will distibute:

```cli
RunInDocker> docker build . -t my-server-tests

RunInDocker> docker run my-server-tests
Microsoft(R) Testing Platform Execution Command Line Tool
Version: 1.0.0-preview.23622.9+fe96e7475 (UTC 2023/12/22)
RuntimeInformation: linux-x64 - .NET 8.0.0
Copyright(c) Microsoft Corporation.  All rights reserved.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:8080
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /test/test
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 GET http://localhost:8080/hello - - -
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[0]
      Executing endpoint 'HTTP: GET /hello'
info: Microsoft.AspNetCore.Routing.EndpointMiddleware[1]
      Executed endpoint 'HTTP: GET /hello'
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 GET http://localhost:8080/hello - 200 - text/plain;+charset=utf-8 73.5556ms
Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 1.7s - MyServer.Tests.dll (linux-x64 - .NET 8.0.0)
```
