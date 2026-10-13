# RFC 010 - Map not runnable tests to failed via runsettings

> **Current implementation note (2026-10-09):** The setting still defaults to true,
> but discovery can reject invalid signatures before an execution outcome exists;
> mapping does not guarantee every invalid method appears as a failed test.
> `TreatDiscoveryWarningsAsErrors` now defaults to true independently.
> See [MSTEST-015](../specifications/mstest.md#mstest-015--outcome-defaults-are-not-discovery-inclusion-rules).
> The XML root below uses the current case-sensitive spelling; no tests were executed.

- [x] Approved in principle
- [x] Under discussion
- [x] Implementation
- [x] Shipped

## Motivation

Some tests which have incompatible signature and cannot be executed are skipped with warnings being thrown.
These tests will now be marked failed since these are not even being executed. User should be able to configure to not fail a test if it is not runnable in accordance with maintaining backward compatibility.

### Proposed solution

Make this setting configurable via MapNotRunnableToFailed tag which is part of the adapter node in the runsettings.

Here is a sample runsettings:

```xml
<RunSettings>
  <MSTestV2> 
    <MapNotRunnableToFailed>true</MapNotRunnableToFailed>   
  </MSTestV2> 
</RunSettings>
```

### Honoring the settings

- If no settings are provided in runsettings, default MapNotRunnableToFailed is set to true.
  This has been kept to fail the tests which cannot be executed and avoid silent failures.
- The setting can be overridden by specifying `<MapNotRunnableToFailed>false</MapNotRunnableToFailed>` in the adapter settings.

## Unresolved questions

None.
