# RFC 018 - Native Microsoft.Testing.Platform integration for MSTest (retire the VSTest bridge)

- [ ] Approved in principle
- [x] Under discussion
- [ ] Implementation
- [ ] Shipped

## Summary

When MSTest runs on Microsoft.Testing.Platform (MTP) today, it does **not** talk to MTP
directly. It goes through `Microsoft.Testing.Extensions.VSTestBridge`, which re-materializes the
VSTest object model (`TestCase`, `TestResult`, `IFrameworkHandle`, `IRunContext`,
`IDiscoveryContext`, `ITestCaseDiscoverySink`, `IMessageLogger`, `IRunSettings`) so the existing
VSTest-shaped adapter can be reused, and then converts the VSTest results back into MTP
`TestNode`/`TestNodeUpdateMessage` objects on the way out.

This RFC proposes to remove that indirection for MSTest and plug MSTest into MTP **natively** as
a first-class `ITestFramework`, producing `TestNode`s directly from MSTest's own neutral
execution model. The VSTest adapter (`MSTest.TestAdapter`'s `ITestDiscoverer`/`ITestExecutor`)
stays for the VSTest host; the bridge is retired only from the MTP code path for MSTest.

This is a **staged** effort. Each phase is independently shippable, keeps the bridge as the
default until the native path reaches parity, and can be validated by the existing acceptance
suite before the next phase begins.

## Motivation

The bridge was the right call to get MSTest onto MTP quickly with maximum reuse. It now costs us:

1. **A double conversion on every discovery and every result.** MSTest produces a neutral
   `UnitTestElement` / framework `TestResult`, the adapter boundary converts it to a VSTest
   `TestCase` / `TestResult`, and `ObjectModelConverters.ToTestNode` immediately converts that
   back into an MTP `TestNode`. The VSTest object model is a pure intermediary that carries no
   information MSTest does not already have.

2. **Information loss.** Because the intermediary is VSTest's `TestCase`, data that MSTest knows
   but VSTest's model cannot carry is dropped. The clearest example is in
   `MSTestBridgedTestFramework.GetMethodIdentifierPropertyFromManagedTypeAndManagedMethod`, where
   `TestMethodIdentifierProperty` is built with `assemblyFullName: string.Empty` and
   `returnTypeFullName: string.Empty` and an inline comment:
   *"In the context of the VSTestBridge where we only have access to VSTest object model, we
   cannot determine ReturnTypeFullName ... the eventual goal should be to stop using the
   VSTestBridge altogether."*

3. **A large surface of adapter shims** whose only purpose is to fake VSTest contracts on top of
   MTP: `RunContextAdapter`, `DiscoveryContextAdapter`, `FrameworkHandlerAdapter`,
   `MessageLoggerAdapter`, `TestCaseDiscoverySinkAdapter`, `RunSettingsAdapter`,
   `RunSettingsPatcher`, `ContextAdapterBase`, plus the `VSTestDiscover/RunTestExecutionRequest`
   factories. Every MTP concept (filtering, configuration, output, messages) is round-tripped
   through an XML/`IRunSettings`/`ITestCaseFilterExpression` representation and back.

4. **Coupling to the VSTest package closure** (`Microsoft.TestPlatform.ObjectModel`) on a code
   path where MTP already provides everything needed.

Crucially, the investigation behind this RFC found that the VSTest object model is **not** threaded
through MSTest execution. It exists only at the very edges and is wrapped immediately into neutral
abstractions that the engine already uses. That makes a native integration a boundary-replacement,
not an engine rewrite.

## Current architecture (MTP path)

```text
MTP ITestFramework request (Discover/Run)  ──►  Microsoft.Testing.Extensions.VSTestBridge
                                                     │  builds VSTest request + context/handle/sink
                                                     ▼
   VSTestDiscover/RunTestExecutionRequest  ──►  MSTestBridgedTestFramework
                                                     │  isMTP: true
                                                     ▼
                        MSTestDiscoverer / MSTestExecutor   (VSTest ITestDiscoverer/ITestExecutor)
                                                     │  wrap VSTest types into NEUTRAL seams:
                                                     │    frameworkHandle.ToTestResultRecorder()
                                                     │    discoverySink.ToUnitTestElementSink()
                                                     │    logger.ToAdapterMessageLogger()
                                                     │    new TestElementFilterProvider(runContext)
                                                     ▼
                        TestExecutionManager / UnitTestDiscoverer   (engine — NEUTRAL)
                                                     │  operates on UnitTestElement + framework TestResult
                                                     ▼
   (results) framework TestResult ──► VSTest TestResult ──► ObjectModelConverters.ToTestNode
                                                     ▼
                        TestNodeUpdateMessage  ──►  MTP IMessageBus
```

The neutral seams already exist in `src\Adapter\MSTestAdapter.PlatformServices\Interfaces`:

| Neutral seam | Role | VSTest impl today |
| --- | --- | --- |
| `IUnitTestElementSink` | receives discovered `UnitTestElement`s | `UnitTestElementSinkExtensions.HostDiscoverySink` |
| `ITestResultRecorder` | receives start/end/`FrameworkTestResult` | `TestResultRecorderExtensions.HostTestResultRecorder` |
| `IAdapterMessageLogger` | diagnostic/info/warn/error text | `AdapterMessageLoggerExtensions` |
| `ITestElementFilterProvider` | supplies the test filter | `TestElementFilterProvider` (wraps `IRunContext`/`IDiscoveryContext`) |
| `DeploymentContext` | test-run directory + runsettings XML | built inline in `MSTestExecutor` |

The docs on `ITestResultRecorder` already state the design intent: *"The concrete recorder ... is
provided at the adapter boundary."* Native MTP integration = a second concrete implementation of
these seams that emits MTP objects.

## Target architecture (MTP path)

```text
MTP ITestFramework request (Discover/Run)  ──►  MSTestTestFramework : ITestFramework, IDataProducer
                                                     │  reads MTP IServiceProvider directly:
                                                     │    IMessageBus, ITestExecutionFilter,
                                                     │    IConfiguration, IOutputDevice, ILoggerFactory,
                                                     │    ICommandLineOptions, ITrxReportCapability
                                                     ▼
                        MSTest engine (unchanged) driven through NEUTRAL seams:
                          - MtpUnitTestElementSink  : IUnitTestElementSink   -> TestNodeUpdateMessage
                          - MtpTestResultRecorder   : ITestResultRecorder    -> TestNodeUpdateMessage
                          - MtpAdapterMessageLogger : IAdapterMessageLogger  -> IOutputDevice / ILogger
                          - MtpTestElementFilter    : ITestElementFilterProvider (from ITestExecutionFilter)
                          - DeploymentContext        from IConfiguration (results dir + runsettings)
                                                     ▼
                        TestNodeUpdateMessage  ──►  MTP IMessageBus     (no VSTest object model)
```

`ObjectModelConverters.ToTestNode`'s property mapping (outcome, timing, standard out/err, TRX
categories/messages/exception, attachments, file location, traits/metadata) is preserved, but
sourced from `UnitTestElement` + `FrameworkTestResult` instead of VSTest `TestCase`/`TestResult`.

## Design principles

1. **Engine is untouched.** `TestExecutionManager`, `UnitTestRunner`, `TestMethodRunner`,
   `UnitTestDiscoverer`, `AssemblyEnumeratorWrapper` keep operating on `UnitTestElement` and the
   framework `TestResult`. This RFC only replaces the *host boundary*.
2. **Additive and reversible per phase.** The native path is introduced behind the existing
   `isMTP` seam / a feature switch and does not remove the bridge until it reaches full parity.
   The VSTest adapter path (real VSTest host) is unaffected throughout.
3. **No behavior change for users.** `TestNode` shape, UIDs, TRX output, filtering semantics,
   runsettings handling, `--filter`/`--treenode-filter`, exit codes, and terminal output must be
   byte-for-byte equivalent, verified by the acceptance suite.
4. **Reuse, don't fork.** The `TestResult -> TestNode` property mapping currently in
   `ObjectModelConverters` is moved (not duplicated) into a neutral converter that takes MSTest
   models, so there is a single source of truth.
5. **Close the fidelity gaps the bridge forced.** Populate `TestMethodIdentifierProperty`
   `AssemblyFullName` and `ReturnTypeFullName`, which the VSTest intermediary cannot carry.

## Phasing

| Phase | Deliverable | Removes |
| --- | --- | --- |
| 1 | Neutral `TestNode` production: `MtpUnitTestElementSink` + `MtpTestResultRecorder` implementing `IUnitTestElementSink`/`ITestResultRecorder` and emitting `TestNodeUpdateMessage`. Shared converter that maps `UnitTestElement`/`FrameworkTestResult` -> `TestNode` (moved out of `ObjectModelConverters`). Covered by unit tests. | Result/discovery round-trip through VSTest `TestResult`/`TestCase` |
| 2 | `MSTestTestFramework : ITestFramework, IDataProducer` that handles MTP `DiscoverTestExecutionRequest`/`RunTestExecutionRequest` directly and drives the engine through the Phase 1 seams. Registered behind a switch; bridge remains the default. | `VSTestDiscover/RunTestExecutionRequest` factories, `MSTestBridgedTestFramework` on the MTP path |
| 3 | Native filtering: build `ITestElementFilterProvider` from MTP `ITestExecutionFilter` (`TestNodeUid`, tree-node, `--filter`) without the VSTest `ITestCaseFilterExpression` round-trip in `ContextAdapterBase`. | `RunContextAdapter`/`DiscoveryContextAdapter` filter path |
| 4 | Native configuration: `DeploymentContext`, results directory, `--testRunParameters`, runsettings env-var provider, and unsupported-entry warnings sourced from MTP `IConfiguration`/command-line providers. | `RunSettingsAdapter`, `RunSettingsPatcher`, `RunSettingsConfigurationProvider`, `RunSettingsEnvironmentVariableProvider` (bridge copies) |
| 5 | Capability rewiring: TRX (`ITrxReportCapability`), graceful stop, banner, telemetry wired directly to the native framework; enrich `TestMethodIdentifierProperty` (`AssemblyFullName`, `ReturnTypeFullName`). | Bridge base classes `VSTestBridgedTestFrameworkBase`, `SynchronizedSingleSessionVSTestBridgedTestFramework` (MSTest usage) |
| 6 | Flip the default to native, delete the MSTest→bridge glue, drop the `Microsoft.Testing.Extensions.VSTestBridge` `PackageReference`/`ProjectReference` from MSTest. | The bridge dependency for MSTest |

`Microsoft.Testing.Extensions.VSTestBridge` itself is **not** deleted by this RFC — NUnit, Expecto,
and third-party VSTest adapters still consume it. Only MSTest stops depending on it.

## Capability & feature parity checklist

Everything the bridge provides on the MSTest path must have a native owner before Phase 6:

| Feature | Bridge component today | Native owner |
| --- | --- | --- |
| Discovery → `TestNode` | `TestCaseDiscoverySinkAdapter` + `ObjectModelConverters` | `MtpUnitTestElementSink` (Phase 1) |
| Results → `TestNode` | `FrameworkHandlerAdapter` + `ObjectModelConverters` | `MtpTestResultRecorder` (Phase 1) |
| Outcome/timing/std out+err/attachments | `ObjectModelConverters.ToTestNode` | shared neutral converter (Phase 1) |
| TRX properties (category/message/exception/type name) | `ObjectModelConverters` + `IInternalVSTestBridgeTrxReportCapability` | shared converter + `MSTestCapabilities` (Phase 1/5) |
| `--filter` / tree-node / uid filter | `TestCaseFilterCommandLineOptionsProvider`, `ContextAdapterBase` | native filter mapping (Phase 3) |
| `--runsettings` / `.runsettings` | `RunSettingsCommandLineOptionsProvider`, `RunSettingsAdapter`, `RunSettingsPatcher` | native config (Phase 4) |
| `--testRunParameters` | `TestRunParametersCommandLineOptionsProvider` | native config (Phase 4) |
| Results directory | `RunSettingsConfigurationProvider` | native config (Phase 4) |
| Runsettings env vars | `RunSettingsEnvironmentVariableProvider` | native provider (Phase 4) |
| Graceful stop | `MSTestGracefulStopTestExecutionCapability` (already MTP-native) | keep as-is |
| Banner | `MSTestBannerCapability` (already MTP-native) | keep as-is |
| Telemetry | `MSTestBridgedTestFramework.CreateTelemetrySender` (already MTP-native) | keep as-is |
| Single-session guard | `SynchronizedSingleSessionVSTestBridgedTestFramework` | small native equivalent (Phase 5) |
| `TestMethodIdentifierProperty` | `MSTestBridgedTestFramework.AddAdditionalProperties` (lossy) | native, full-fidelity (Phase 5) |

## Testing strategy

- **Golden-output parity.** The `HelpInfoTests`, TRX, terminal-reporter, filtering, runsettings,
  and discovery acceptance tests under
  `test/IntegrationTests/MSTest.Acceptance.IntegrationTests` and
  `test/IntegrationTests/Microsoft.Testing.Platform.Acceptance.IntegrationTests` are the parity
  oracle. Each phase must keep them green (after `.\build.cmd -pack`).
- **Dual-run during transition.** While both paths exist, run the MSTest acceptance suite against
  the bridge and against the native path (feature switch) to catch divergence early.
- **Unit tests** for the new neutral converter and the `IUnitTestElementSink`/`ITestResultRecorder`
  MTP implementations in `MSTestAdapter.PlatformServices.UnitTests` / `MSTestAdapter.UnitTests`.
- **Public API.** No new *public* MSTest/MTP API is expected; the native framework and seams are
  `internal`. Any unavoidable public addition goes through `PublicAPI.Unshipped.txt`.

## Risks

1. **Silent output divergence** (e.g. TRX `FullyQualifiedTypeName`, UID stability, std out/err
   joining). Mitigated by golden-output acceptance parity and moving—not rewriting—the mapping.
2. **Filter-semantics drift** between VSTest filter expressions and native MTP filters. Phase 3 is
   isolated and gated behind parity tests specifically for `--filter`/tree-node behavior.
3. **Runsettings edge cases** (data collectors, loggers, deprecated `RunConfiguration` entries the
   bridge warns about). Phase 4 must reproduce the same warnings and same ignored-entry behavior.
4. **In-flight refactor overlap.** There is already active work removing the VSTest object model
   from platform services (referenced in `UnitTestElementSinkExtensions`/`ITestResultRecorder`
   remarks). This RFC must be sequenced with that work to avoid conflicting boundaries; Phase 1
   deliberately builds on those neutral seams rather than around them.
5. **UWP/WinUI targets.** The MTP path is `#if !WINDOWS_UWP`; the native framework must preserve
   the same conditional compilation and not regress those TFMs.

## What is intentionally not done

- **Not deleting `Microsoft.Testing.Extensions.VSTestBridge`.** Other adapters depend on it.
- **Not touching the VSTest host path.** `MSTestDiscoverer`/`MSTestExecutor` continue to implement
  VSTest's `ITestDiscoverer`/`ITestExecutor` for `vstest.console`.
- **Not changing the MSTest execution engine.** `UnitTestElement`/`FrameworkTestResult` remain the
  internal currency.
- **No user-facing behavior change.** This is an internal integration change; parity is the bar.

## Open questions

1. Rollout switch: env var, `runsettings`/`testconfig` entry, or a hard cutover once parity CI is
   green? Proposal: internal feature switch during Phases 2–5, hard default flip in Phase 6.
2. Should the shared `UnitTestElement`/`FrameworkTestResult` → `TestNode` converter live in
   `MSTest.TestAdapter` (MTP-facing folder) or in `MSTestAdapter.PlatformServices`? Proposal: the
   MTP-facing adapter folder, next to the other MTP-native capability code.
3. Coordination point with the ongoing "remove VSTest object model from platform services" work —
   who owns the neutral-seam contract to avoid churn.
