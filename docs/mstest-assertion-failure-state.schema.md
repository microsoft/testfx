# MSTest assertion failure state JSON schema

[`mstest-assertion-failure-state.schema.json`](./mstest-assertion-failure-state.schema.json) is the
JSON Schema (draft-07) for artifacts produced when
`mstest:execution:captureAssertionFailureDiagnostics` is enabled.

The artifact is versioned through its required `schemaVersion` property. Breaking contract changes
must increment that value and publish a corresponding schema.

## Publishing to SchemaStore

SchemaStore can associate the generated artifacts by filename without changing their content. Open a
PR against [`SchemaStore/schemastore`](https://github.com/SchemaStore/schemastore) and add this entry
to the alphabetically sorted `schemas` array in
[`src/api/json/catalog.json`](https://github.com/SchemaStore/schemastore/blob/master/src/api/json/catalog.json):

```json
{
  "name": "MSTest assertion failure state",
  "description": "Diagnostic state captured by MSTest when an assertion fails.",
  "fileMatch": ["mstest-assertion-failure-state-attempt-*-invocation-*-capture-*.json"],
  "url": "https://raw.githubusercontent.com/microsoft/testfx/main/docs/mstest-assertion-failure-state.schema.json"
}
```

The schema covers assertion values, test and thread identity, concurrently active tests, managed
stack frames, runtime and operating-system details, current cultures, process CPU and memory
measurements, process I/O, and output-volume capacity.
