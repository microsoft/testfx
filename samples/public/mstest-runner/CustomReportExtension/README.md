# Custom report extension

This sample uses a manually created entry point to register MSTest and a composite
Microsoft.Testing.Platform extension. `TestResultConsoleReporter` consumes test-node updates
and handles test-session lifetime events to print results to the console.

See [`Program.cs`](CustomReportExtension/Program.cs) for the registration and
[`TestResultConsoleReporter.cs`](CustomReportExtension/TestResultConsoleReporter.cs) for the extension.
