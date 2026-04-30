# Glossary

This glossary defines key terms and concepts used throughout the MSTest and Microsoft.Testing.Platform (MTP) documentation and codebase.

## A

### ArgumentArity

An MTP struct (`ArgumentArity.cs`) that defines the minimum and maximum number of values a command-line option accepts. Provides five predefined constants: `Zero` (0,0), `ZeroOrOne` (0,1), `ZeroOrMore` (0,∞), `ExactlyOne` (1,1), and `OneOrMore` (1,∞). Used by `ICommandLineOptionsProvider` implementations to declare option shapes.

### AzureDevOpsReport

An MTP extension (`Microsoft.Testing.Extensions.AzureDevOpsReport`) that formats and reports test results to Azure DevOps pipelines. It generates pipeline-compatible output including TFM and test name details for richer CI reporting.

## C

### CrashDump

An MTP extension (`Microsoft.Testing.Extensions.CrashDump`) that automatically captures a process memory dump when the test host crashes. Useful for diagnosing unexpected process termination during test runs.

## D

### DelayBackoffType

A public enum in the `Microsoft.VisualStudio.TestTools.UnitTesting` namespace that specifies the delay strategy used between retries by the `[Retry]` attribute. Values: `Constant` (fixed delay between each attempt) and `Exponential` (delay doubles with each attempt: base × 2^(n−1)).

## F

### FQN (Fully Qualified Name)

A unique string that identifies a test by its complete namespace, class, and method path (e.g., `MyNamespace.MyClass.MyTestMethod`). Used in IDE integration and JSON-RPC protocol messages to unambiguously reference individual tests.

### Formal Verification (FV)

The practice of using formal mathematical proofs to establish correctness properties of code. In this project, FV uses [Lean 4](#lean-4) to prove properties about selected MTP components (see [FV Target](#fv-target)). FV artifacts live in the `formal-verification/` directory.

### FV Target

A specific code component (function, struct, or class) selected for formal verification. Each FV target progresses through defined phases: (1) identified, (2) informal spec extracted, (3) Lean 4 formal spec written, (4) implementation model extracted, (5) proofs completed. Current targets are listed in `formal-verification/TARGETS.md`.

## H

### HangDump

An MTP extension (`Microsoft.Testing.Extensions.HangDump`) that captures a process memory dump when a test exceeds a configured timeout. Helps diagnose deadlocks, infinite loops, or unexpectedly slow tests.

## I

### Informal Spec (FV)

An intermediate artifact in the [Formal Verification (FV)](#formal-verification-fv) workflow that documents the behavioural properties of an [FV Target](#fv-target) in plain English (or structured natural language), before writing formal [Lean 4](#lean-4) proofs. An informal spec lists preconditions, postconditions, edge-case expectations, and any confirmed bugs discovered during analysis. It corresponds to Phase 2 of the FV target lifecycle and lives in the `formal-verification/specs/` directory.

### IsTestingPlatformApplication

An MSBuild property (`<IsTestingPlatformApplication>true</IsTestingPlatformApplication>`) that marks a project as an MTP test application. When set, the project builds into a self-contained test runner executable rather than a class library consumed by a separate test host.

## J

### JSON-RPC Protocol

The communication protocol used between a test runner executable (server) and a client (IDE, CLI, or CI tool). Based on [JSON-RPC 2.0](https://www.jsonrpc.org/specification), it defines messages for test discovery (`testing/discoverTests`), test execution (`testing/runTests`), result reporting, debugger attachment, and telemetry.

## L

### Lean 4

A theorem prover and interactive proof assistant used in this project for [Formal Verification (FV)](#formal-verification-fv). Lean 4 proofs are written in the `formal-verification/lean/FVSquad/` directory and compiled via `lake build`. The CI workflow (`lean-proofs.yml`) automatically builds and checks these proofs.

### Lean Squad

An automated agentic workflow (`.github/workflows/lean-squad.md`) that manages the formal verification lifecycle for this project. It identifies [FV Targets](#fv-target), extracts informal specs, writes [Lean 4](#lean-4) formal models, and maintains the Lean–C# correspondence documentation.

## M

### MSTest

Microsoft's unit testing framework for .NET. Provides attributes (`[TestClass]`, `[TestMethod]`, `[DataRow]`, etc.), assertions (`Assert`, `CollectionAssert`), and lifecycle hooks for writing and organizing tests. Packaged as `MSTest.TestFramework`, `MSTest.TestAdapter`, `MSTest.Analyzers`, and `MSTest.Sdk`.

### MSTest Runner

The self-contained test runner mode for MSTest, built on top of Microsoft.Testing.Platform. When `IsTestingPlatformApplication` is set, the test project compiles into a standalone executable that runs tests directly without requiring `dotnet test` or `vstest.console`.

### MSTest.Sdk

A meta-package that bundles `MSTest.TestFramework`, `MSTest.TestAdapter`, and `MSTest.Analyzers` with default MSBuild SDK configuration. Simplifies project setup by providing a single package reference.

### MTP

See **Microsoft.Testing.Platform**.

### Microsoft.Testing.Platform (MTP)

A lightweight, extensible test platform for .NET that serves as a modern alternative to VSTest. MTP ships as a NuGet package (`Microsoft.Testing.Platform`) and provides the core infrastructure for running tests: command-line parsing, test session management, result reporting, and an extension model. Test frameworks (e.g., MSTest, xUnit adapters) and extensions (e.g., CrashDump, HangDump) plug into MTP.

## N

### NopFilter

A built-in MTP test filter that matches no tests. Used primarily in scenarios where filtering is required by the API but no tests should be selected (e.g., for dry-run or diagnostic purposes).

## O

### Orchestrator

A component in MTP that coordinates multi-process test execution. The orchestrator manages the lifecycle of one or more test host processes, aggregates results, and handles communication between the outer process (e.g., `dotnet test`) and the inner test runner processes.

### OpenTelemetry extension

An MTP extension (`Microsoft.Testing.Extensions.OpenTelemetry`) that exports test session telemetry using the [OpenTelemetry](https://opentelemetry.io/) standard, enabling integration with distributed tracing and observability platforms.

## R

### Retry

An MTP extension (`Microsoft.Testing.Extensions.Retry`) that automatically re-runs failed tests a configurable number of times. Useful for reducing flakiness in CI environments.

### RFC

Request for Comments document in the `docs/RFCs/` folder. RFCs describe design decisions, proposed features, and implementation details for MSTest and MTP.

## T

### TFM (Target Framework Moniker)

A short string that identifies a specific .NET target framework (e.g., `net9.0`, `net48`, `netstandard2.0`). Used to distinguish test runs across multiple frameworks in multi-targeted projects.

### TrxReport

An MTP extension (`Microsoft.Testing.Extensions.TrxReport`) that generates a `.trx` (Test Results XML) file upon test session completion. TRX files are the standard Visual Studio and Azure DevOps test result format.

### TRX (Test Results XML)

The XML-based test result file format used by Visual Studio, Azure DevOps, and `vstest.console`. Contains test run metadata, individual test outcomes, error messages, and stack traces. Generated by the **TrxReport** extension.

### TreeNodeFilter

An MTP component (`TreeNodeFilter.cs`) that evaluates filter expressions against test node properties to select which tests to run. Filter expressions support Boolean algebra: `&` (AND), `|` (OR), `!` (NOT), and property comparisons (e.g., `FullyQualifiedName~MyTest`). Internally, filter expressions are parsed into a `FilterExpression` tree and evaluated recursively.

## V

### VSTest

Microsoft's previous-generation test platform (`vstest.console.exe`, `Microsoft.TestPlatform.*`). MSTest v2 originally ran on top of VSTest. MTP is the modern successor to VSTest, offering better performance and a simplified extension model.

### VSTestBridge

An MTP extension (`Microsoft.Testing.Extensions.VSTestBridge`) that provides backward compatibility for test adapters written against the VSTest API. Allows existing VSTest-based test frameworks and adapters to run on MTP without a full rewrite.
