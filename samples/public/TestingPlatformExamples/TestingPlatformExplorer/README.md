# Testing platform explorer

This project demonstrates building a test application directly on Microsoft.Testing.Platform.
[`Program.cs`](Program.cs) registers a small custom testing framework, a TRX reporter,
in-process lifecycle handlers and data consumers, and out-of-process host controllers.
The example tests in [`UnitTests.cs`](UnitTests.cs) include passing, failing, and skipped
outcomes so you can observe the extension callbacks.
