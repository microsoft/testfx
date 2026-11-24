# RFC 008 - Test case timeout via runsettings

> **Current implementation note (2026-10-09):** Timeout omission retains the internal
> no-timeout default, but an explicit `0` or negative configured timeout is rejected with
> a warning and ignored. Use the case-sensitive `RunSettings` root; `MSTestV2` remains an
> alias for `MSTest`. Attribute precedence remains. See [MSTEST-014](../specifications/mstest.md#mstest-014--timeout-omission-differs-from-an-explicit-zero)
> and the [schema guide](../testconfig.schema.md#timeout-keys-must-be-strictly-positive).
> This is source inspection/test mapping, not timeout conformance across every host/TFM.

- [x] Approved in principle
- [x] Under discussion
- [x] Implementation
- [x] Shipped

## Motivation

User should be able to configure global test case timeout for all the test cases part of the run.

### Proposed solution

Make test case timeout configurable via TestTimeout tag which is part of the adapter node in the runsettings.

Here is a sample runsettings:

```xml
<RunSettings>
  <MSTestV2> 
    <TestTimeout>5000</TestTimeout>   
  </MSTestV2> 
</RunSettings>
```

### Honoring the settings

- If no settings are provided in runsettings, default timeout is set to 0.
- Timeout specified via Timeout attribute on TestMethod takes precedence over the global timeout specified via runsettings.
- For all the test methods that do not have Timeout attribute, timeout will be based on the timeout specified via runsettings.

## Unresolved questions

None.
