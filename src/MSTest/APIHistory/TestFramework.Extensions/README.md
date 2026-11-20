# Retired Extensions API provenance

These are the unchanged shipped and unshipped API records of
`MSTest.TestFramework.Extensions` at the rebased shared-core prerequisite
`984c2c635742d3d9699f488af36d25dff48e0f85`. They are historical records, not an
active build input or an API-compatibility claim for the new assemblies.
The `.txt.history` suffix deliberately keeps these snapshots out of
`eng/mark-shipped.ps1`'s recursive `PublicAPI.Shipped.txt` /
`InternalAPI.Shipped.txt` discovery, so a release cannot promote or rewrite them.

The non-UI APIs moved to core in #12412 and #12428. The dedicated
`TestFramework.Compatibility` project retains independent live copies of the
ordinary non-UI API baselines and forwards those types to core. The remaining UI
APIs now belong to the `MSTest.Uwp.dll` and `MSTest.WinUI.dll` assemblies, which
`MSTest.TestFramework` references for UWP and WinUI projects. UI consumers must
update their assembly references and recompile for this intentional next-major
assembly boundary.
