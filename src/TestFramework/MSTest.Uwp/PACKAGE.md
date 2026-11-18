# MSTest.Uwp

Write and run UI-thread tests in classic and modern UWP applications. This
package contains `MSTest.Uwp.dll`, the UWP MSTest adapter and platform services,
and their localized resources. It depends on the matching `MSTest.TestFramework`
package, not on `MSTest.TestAdapter` or `MSTest.WinUI`.

Supported targets are classic `uap10.0.16299` and modern
`net10.0-windows10.0.18362.0`, including compatible .NET 11 UWP consumers.
Modern consumers targeting Windows 17763 must raise their Windows target to
18362 or later.
The application still owns its UWP tooling, runtime, manifest and activation.
Modern UWP shares the adapter binaries with WinUI but retains its own UWP
framework and does not depend on `MSTest.WinUI` or the Windows App SDK.

Reference this package instead of the `MSTest` metapackage or ordinary adapter
for modern UWP. `MSTest.Sdk` selects it from the existing UWP tooling properties,
including early `UseUwp` when desktop tooling is imported later.
Ordinary helper libraries need only `MSTest.TestFramework`.

`Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.UITestMethodAttribute`
keeps its existing API and dispatches through the UWP CoreWindow dispatcher.
Migrate references to the retired `MSTest.TestFramework.Extensions` assembly and
recompile. All six `TestContext` directory getters return null in UWP hosts,
even when the property bag contains same-named values. Deployment items are
unsupported: modern UWP tests using `[DeploymentItem]` fail before their body;
classic UWP's netstandard core does not expose that attribute.

MSTest is Microsoft supported Test Framework.
