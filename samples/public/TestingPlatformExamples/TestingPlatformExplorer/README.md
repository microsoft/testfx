# Testing platform explorer

This project demonstrates building a test application directly on Microsoft.Testing.Platform.
[`Program.cs`](Program.cs) registers a small custom testing framework, a TRX reporter,
in-process lifecycle handlers and data consumers, and out-of-process host controllers.
The example tests in [`UnitTests.cs`](UnitTests.cs) include passing, failing, and skipped
outcomes so you can observe the extension callbacks.

The repository's [experimental Bazel build](../../../../docs/bazel.md) exposes
this sample as `//:explorer_released` (released packages) and `//:explorer`
(source-built MTP). From the repository root, use
`.\eng\bazel.ps1 run //:explorer '--' --list-tests` for discovery or
`.\eng\bazel.ps1 test //:explorer_test` for its passing smoke case. The other
sample outcomes remain intentionally failing or skipped.
