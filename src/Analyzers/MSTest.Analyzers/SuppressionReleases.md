# Suppression releases

Roslyn's analyzer release tracker only supports diagnostic descriptors, not suppression descriptors.
Keep suppressor IDs in this catalog rather than in `AnalyzerReleases.Shipped.md` or
`AnalyzerReleases.Unshipped.md`, so diagnostic release-tracking checks remain enabled.
`AnalyzerReleaseTrackingTests` verifies the suppression IDs and their suppressed diagnostic IDs.
Add new suppressors under an `Unshipped` heading, then move them to their release heading when shipped.

## Release 3.10.0

Suppression ID | Suppressed diagnostic ID | Notes
--------------|--------------------------|-------
MSTEST0047 | IDE0060 | UnusedParameterSuppressor, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0047)

## Release 3.6.0

Suppression ID | Suppressed diagnostic ID | Notes
--------------|--------------------------|-------
MSTEST0033 | CS8618 | NonNullableReferenceNotInitializedSuppressor, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0033)

## Release 3.5.0

Suppression ID | Suppressed diagnostic ID | Notes
--------------|--------------------------|-------
MSTEST0027 | VSTHRD200 | UseAsyncSuffixTestMethodSuppressor, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0027)
MSTEST0028 | VSTHRD200 | UseAsyncSuffixTestFixtureMethodSuppressor, [Documentation](https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0028)
