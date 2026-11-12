# Public samples

## Observability

| Scenario | Sample |
| --- | --- |
| Use an application-owned `HostApplicationBuilder` to supply configuration and logging, collect MTP diagnostics with OpenTelemetry, and preserve the application's resource identity and service ownership | [`MTPOTel`](MTPOTel) |

## Test reliability

| Scenario | Sample |
| --- | --- |
| Engineer a deterministic parallel MSTest suite using isolation, `[DoNotParallelize]` or `[ResourceLock]`, conditional tests, cooperative timeouts, retries as a last resort, and seeded test-order randomization | [`ReliableTestSuite`](ReliableTestSuite) |

## MSTest.Sdk and runner

| Scenario | Sample |
| --- | --- |
| Compare MSTest.Sdk configurations for the default runner, VSTest, Native AOT, Playwright, Aspire, and Windows UI Automation | [`DemoMSTestSdk`](DemoMSTestSdk) |
| Explore custom runner extensions, explicit entry points, Native AOT, Docker execution, and runner comparisons | [`mstest-runner`](mstest-runner) |

## Microsoft.Testing.Platform basics

| Scenario | Sample |
| --- | --- |
| Explore a custom testing framework and in-process and out-of-process platform extensions | [`TestingPlatformExamples/TestingPlatformExplorer`](TestingPlatformExamples/TestingPlatformExplorer) |

## Application hosting

| Scenario | Sample |
| --- | --- |
| Generate an MTP entry point that starts a real ASP.NET Core application, injects host services into MSTest test classes, runs against its endpoint, and reuses the web host's configuration, logging, DI, and OpenTelemetry pipeline | [`MTPHostIntegration/AspNetCoreTests`](MTPHostIntegration/AspNetCoreTests) |
| Generate an MTP entry point for an Aspire AppHost test with host-service injection while the process uses the same ServiceDefaults pattern and owns a single host/provider pipeline | [`MTPHostIntegration/AspireTests`](MTPHostIntegration/AspireTests) |

## Windows application testing

The sample name identifies the Windows application model, test host, and packaging mode.

| Application model | Test host | Packaging | Sample | Status |
| --- | --- | --- | --- | --- |
| UWP | VSTest | Packaged | [`UwpVSTestApp`](UwpVSTestApp) | Legacy supported configuration |
| Modern UWP (`UseUwp=true`) | Microsoft.Testing.Platform (MTP) | Packaged AppContainer | [`UwpMtpApp`](UwpMtpApp) | Supported and the MSTest.Sdk default |
| Classic UWP (`uap10.0`) | Microsoft.Testing.Platform (MTP) | Packaged AppContainer | [`ClassicUwpMtpApp`](ClassicUwpMtpApp) | Supported for existing classic projects |
| WinUI 3 | VSTest | Packaged | [`WinUIVSTestApp`](WinUIVSTestApp) | Supported |
| WinUI 3 | VSTest | Unpackaged | — | Not supported; VSTest's WinUI provider requires an AppX manifest |
| WinUI 3 | MTP | Packaged | [`WinUIMtpPackagedApp`](WinUIMtpPackagedApp) | Supported |
| WinUI 3 | MTP | Unpackaged | [`WinUIMtpUnpackagedApp`](WinUIMtpUnpackagedApp) | Supported |
| WinUI 3 `packagedClassicApp` | MTP | Packaged AppContainer | [`WinUIMtpAppContainerApp`](WinUIMtpAppContainerApp) | Supported; run non-elevated |

UWP is always packaged. For more detail about packaging, trust levels, launch behavior, and UI-thread
tests, see [Testing UWP and WinUI apps with MSTest](../../docs/winui-testing.md).

Run commands from a sample directory and select a concrete architecture:

The MTP `dotnet test --project` syntax used by the WinUI samples requires .NET SDK 10 or later for
the native runner and CLI. `WinUIMtpUnpackagedApp` is a framework-dependent
`net10.0-windows10.0.19041.0` application and requires the .NET 10 Desktop Runtime for the
selected architecture, included with the Windows .NET 10 SDK. This runtime prerequisite is specific to the unpackaged sample: the
packaged WinUI MTP samples are self-contained, and app-model controllers roll forward to newer
installed .NET major versions. The modern UWP sample targets .NET 10 and also requires .NET SDK 10
or later, but uses Visual Studio desktop MSBuild for UWP build and execution.

```powershell
# Full-trust and unpackaged WinUI MTP samples
dotnet build -p:Platform=x64 -bl:{{}}
dotnet run --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64

# UWP VSTest sample
dotnet test UwpVSTestApp.csproj -p:Platform=x64

# WinUI VSTest sample
dotnet test WinUIVSTestApp.csproj -p:Platform=x64
```

Modern and classic UWP builds require desktop MSBuild from Visual Studio with the Universal Windows
Platform workload. Follow the commands in [`UwpMtpApp/README.md`](UwpMtpApp/README.md) or
[`ClassicUwpMtpApp/README.md`](ClassicUwpMtpApp/README.md). Only the explicit legacy VSTest samples
require the Visual Studio test runtime provider.

### Local-package UWP validation

The two UWP MTP samples include English/French adapter-message checks and real localized manifest
resources. Public samples use their checked-in release pins. For repository packaging experiments,
reuse `ClassicUwpTests` and `ModernUwpTests` in
`test/IntegrationTests/MSTest.Acceptance.IntegrationTests`: these generated equivalents use the
configuration-matched local shipping feed, a fresh fixture-owned package cache, unique package
identities and existing exact-identity cleanup. Do not clear shared caches or manually register the
public sample's fixed identity to compare package states.

Run the following **from the repository root**, only after the repository's Windows app-model
preflight succeeds with the required native XAML/AppX/UWP tools and SDKs. Desktop MSBuild alone is
insufficient. These commands register/activate generated applications; they are not a read-only
preflight. Coordinate with the build owner: never pack/build against shared outputs concurrently.

```powershell
# A complete fresh pack is required; skipping UAP does not validate the classic consumer.
.\build.cmd -pack -c Release -bl
if ($LASTEXITCODE -ne 0) { throw "Pack failed: $LASTEXITCODE" }

$project = 'test\IntegrationTests\MSTest.Acceptance.IntegrationTests\MSTest.Acceptance.IntegrationTests.csproj'
$filter = 'FullyQualifiedName~ClassicUwpTests.ClassicUwp_ConsumesUapAssets_AndRunsPlainAndUiTestsThroughMtp|FullyQualifiedName~ModernUwpTests.ModernUwp_ConsumesUwpAssets_AndRunsPlainAndUiTestsThroughMtp'
$results = Join-Path (Get-Location).Path ('artifacts\TestResults\Release\uwp-packaging-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $results | Out-Null
$previousAppModelGate = [Environment]::GetEnvironmentVariable('TESTFX_RUN_WINDOWS_APP_MODEL_TESTS')
try {
    $env:TESTFX_RUN_WINDOWS_APP_MODEL_TESTS = '1'

    # Builds the acceptance project and independently discovers the planned outer cases.
    & .\.dotnet\dotnet.exe test --project $project -c Release --list-tests text --filter $filter `
        -p:TestingPlatformCommandLineArguments= -p:TestRunnerAdditionalArguments= `
        "-bl:$results\discovery.binlog"
    if ($LASTEXITCODE -ne 0) { throw "Discovery failed: $LASTEXITCODE" }

    & .\.dotnet\dotnet.exe test --project $project -c Release --filter $filter `
        -p:UsingDotNetTest=true "-p:ArtifactsTestResultsDir=$results" `
        "-bl:$results\execution.binlog" --show-test-results all
    if ($LASTEXITCODE -ne 0) { throw "Acceptance execution failed: $LASTEXITCODE" }
}
finally {
    [Environment]::SetEnvironmentVariable('TESTFX_RUN_WINDOWS_APP_MODEL_TESTS', $previousAppModelGate)
}
```

The selector plans **five outer cases**: classic MSTest.Sdk/MSTest/explicit adapter references,
and modern net10/net11. Reconcile discovery with those source-declared rows and parse the fresh
outer TRX using the repository's
[acceptance execution gate](../../.github/skills/testfx-acceptance-validation/SKILL.md#3-run-a-focused-regression-and-verify-execution);
do not infer the expected list from the report being verified. Each runtime consumer independently
requires these six passed child results, with six executed/passed and zero failed/error/
inconclusive/notExecuted counters:

- `PlainTestMethod_RunsInClassicUwpPackage` or `PlainTestMethod_RunsInPackagedUwp`
- `UITestMethod_RunsOnCoreWindowDispatcher`
- `AdapterFailureMessages_AreLocalized_English`
- `AdapterFailureMessages_UseNeutralFallbackForFrench`
- `ApplicationResources_AreLocalized_English`
- `ApplicationResources_AreLocalized_French`

The optional selector
`FullyQualifiedName~ModernUwpTests.ModernUwp_BuildsMtpPackageForArchitecture` adds two build-only
cases, x86 and ARM64. Those guard resolved assets and app-owned `resources.pri`, not native
execution on those architectures.

The packaged adapter guard pins the current UWP MTP satellite suppression: a French runtime UI
still receives the English `AddResultFile("")` message. It does not demand suppressed translations
or remove suppression to make a positive French assertion pass. App-owned PRI resources, in
contrast, must resolve real French values. An experimental layout exposing more adapter languages
is a policy difference to review, not proof that the existing English fallback is a product bug.
VSTest's build-culture satellite filtering is a separate comparison scenario: use matching build
UI cultures and do not infer native-UWP behavior from a non-UWP VSTest run.

Retain package filenames/hashes, source state, `project.assets.json`, both
`resolved-mstest-assets.txt` (compile) and `resolved-mstest-runtime-assets.txt` (copy-local inputs),
the generated manifest/layout, binlogs and TRX separately for each baseline/candidate pack.
Neither package presence nor process exit proves translation loading or correct UWP/WinUI selection.
The guards intentionally do not prescribe a candidate adapter `lib` layout or require a particular
satellite representation. Missing tooling/payloads, skipped tests or incomplete results mean native
validation remains blocked; sample readiness is not a release-ready migration verdict.

AppContainer WinUI uses the sidecar's `InvokeTestingPlatform` MSBuild target rather than native
`dotnet test`; follow [`WinUIMtpAppContainerApp/README.md`](WinUIMtpAppContainerApp/README.md).

When adding a public sample directory, include a link and scenario description in this index.
