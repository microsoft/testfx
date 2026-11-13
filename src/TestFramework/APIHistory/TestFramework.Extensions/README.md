# Retired Extensions API provenance

These are the unchanged shipped and unshipped API records of
`MSTest.TestFramework.Extensions` at merged main
`e1c66e5cbfa844384a59343c3727821f97e2a09a`. They are historical records, not an
active build input or an API-compatibility claim for the new assemblies.
The `.txt.history` suffix deliberately keeps these snapshots out of
`eng/mark-shipped.ps1`'s recursive `PublicAPI.Shipped.txt` /
`InternalAPI.Shipped.txt` discovery, so a release cannot promote or rewrite them.

The non-UI APIs moved to core in #12412 and #12428. The remaining UI APIs now
belong to `MSTest.Uwp` and `MSTest.WinUI`. Consumers must update their package
references and recompile for this intentional next-major package boundary.
