# MSTest.WinUI

Write and run UI-thread tests in packaged and unpackaged WinUI applications.
This package contains `MSTest.WinUI.dll`, the WinUI MSTest adapter and platform
services, and their localized resources. It depends on the matching
`MSTest.TestFramework` package, not on `MSTest.TestAdapter` or `MSTest.Uwp`.

The package targets `net10.0-windows10.0.18362.0` and compatible .NET 11
consumers. The application still owns its Windows App SDK version, runtime
bootstrap, dispatcher, manifest and activation; this package does not install
or select that runtime.

Reference this package instead of the `MSTest` metapackage or ordinary adapter.
`MSTest.Sdk` selects it for WinUI applications, with
`MSTestApplicationModel=WinUI` available for explicit restore-time selection.
Ordinary helper libraries need only `MSTest.TestFramework`.

The existing AppContainer `UITestMethodAttribute` and `WinUITestTargetAttribute`
APIs are unchanged. Migrate references to the retired
`MSTest.TestFramework.Extensions` assembly and recompile. All six `TestContext`
directory getters return null in WinUI hosts, even when the property bag
contains same-named values. Tests using `[DeploymentItem]` fail before their
body because UI-host deployment is unsupported.

MSTest is Microsoft supported Test Framework.
