# Vendored source files

A small set of source files in this repo are copied (verbatim or adapted) from
other repositories — for example, polyfills from
[`dotnet/runtime`](https://github.com/dotnet/runtime) or utility classes from
[`dotnet/roslyn-analyzers`](https://github.com/dotnet/roslyn-analyzers).

We don't want to lose track of these when upstream fixes/updates land. To make
this manageable, the repo tracks each copied file in a manifest and a
[scheduled GitHub Actions workflow](../.github/workflows/check-vendored-files.yml)
opens (or updates) a tracking issue whenever upstream changes are detected.

The implementation is intentionally simple: no auto-merging, no patch
application. The bot's job is to **notify**; a human decides what to port.

## Files involved

| File | Purpose |
| --- | --- |
| [`vendored-files.json`](./vendored-files.json) | Manifest. Source of truth for every tracked file and its baseline. |
| [`../.github/scripts/check_vendored_files.py`](../.github/scripts/check_vendored_files.py) | Drift detection logic (`validate` + `check` modes). |
| [`../.github/workflows/check-vendored-files.yml`](../.github/workflows/check-vendored-files.yml) | Weekly schedule + manual dispatch + PR validation. |

## Manifest schema

```jsonc
{
  "entries": [
    {
      "id": "stable-kebab-case-id",
      "local_path": "src/path/to/Local.cs",
      "notes": "free-form description of local adaptations",
      "sources": [
        {
          "repo": "owner/repo",
          "ref": "main",
          "path": "path/in/upstream/repo.cs",
          "baseline_ref_sha": "<40-char SHA>",   // upstream branch HEAD at last sync
          "baseline_blob_sha": "<40-char SHA>",  // upstream file blob SHA at last sync (primary drift signal)
          "scope": "optional human description (e.g. 'lines 41-59 only')"
        }
      ]
    }
  ]
}
```

A single local file may declare multiple upstream sources (e.g.
[`ResponseFileHelper.cs`](../src/Platform/Microsoft.Testing.Platform/CommandLine/ResponseFileHelper.cs)
combines logic from two `dotnet/command-line-api` files). Each source is
tracked independently.

## How drift is detected

For every `(entry, source)` pair the workflow:

1. Calls `GET /repos/{repo}/contents/{path}?ref={ref}` to get the current blob SHA.
2. Compares it with `baseline_blob_sha`. Equal → no drift, no issue activity.
3. Otherwise fetches the baseline content (via the blobs API, robust to
   force-pushes) and the current content (via `raw.githubusercontent.com`),
   computes a unified diff, and opens/updates a tracking issue labelled
   `area-vendored-sync` containing:
   - links to the upstream file history, baseline blob, current blob, and the
     whole-repo compare URL,
   - the upstream-only diff (truncated at 300 lines),
   - the local adaptation notes,
   - a reconciliation checklist.

Idempotency uses a hidden marker `<!-- vendored-sync:id={id}:{source-index} -->`
in the issue body, not the title. Existing matching issues are updated in place
when the rendered body changes; otherwise they are left alone.

The workflow never auto-closes issues. A reviewer closes the issue after the
reconciliation PR is merged.

## Adding a new vendored file

1. Add a comment in the local file pointing at the upstream URL (existing files
   use `// Copied from <url>` or `// Adapted from <url>`).
2. Add an entry to `eng/vendored-files.json` with a stable kebab-case `id`,
   the local path, and at least one source. Fill in:
   - `baseline_ref_sha`: the upstream branch SHA you copied from
     (`gh api repos/{repo}/commits/{ref} --jq .sha`),
   - `baseline_blob_sha`: the upstream file's blob SHA at that ref
     (`gh api "repos/{repo}/contents/{path}?ref={ref}" --jq .sha`).
3. Run `python .github/scripts/check_vendored_files.py validate` locally to
   confirm the structure is correct.

## Reconciling drift

When a `[vendored-sync]` issue is opened:

1. Read the upstream diff in the issue body and check the file history link.
2. Port the relevant changes to the local file. If the upstream change is
   irrelevant to the copied region (e.g. an unrelated method was modified),
   no code change is required.
3. Update the corresponding `baseline_ref_sha` and `baseline_blob_sha` in
   `eng/vendored-files.json`.
4. Open a PR with the local change (if any) and the manifest bump in a single
   commit. After it merges, close the tracking issue.

If the issue is closed without bumping the manifest, the next scheduled run
will detect the same drift and open a new issue. That is intentional: closing
without a manifest update is a "remind me later" action.

## Excluded files

Some files contain "copied from" comments but are out of scope for this
automation:

- `src/Platform/Microsoft.Testing.Extensions.Retry/RetryOrchestrator.cs` —
  intra-repo copy, no external upstream.
- `src/Platform/Microsoft.Testing.Extensions.Retry/RandomId.cs` —
  intra-repo copy from `test/Utilities/Microsoft.Testing.TestInfrastructure`,
  no external upstream.
- `src/Analyzers/MSTest.SourceGeneration/ObjectModels/DynamicDataTestMethodArgumentsInfo.cs` —
  intra-repo `// Based on DynamicDataSourceType in:` reference to an MSTest
  attribute, no external upstream.
- `src/Adapter/MSTestAdapter.PlatformServices/Discovery/AssemblyEnumeratorWrapper.cs` —
  the line-44 helper is annotated `// Copy from https://stackoverflow.com/a/15608028/...`;
  Stack Overflow snippets don't have a trackable upstream file.
- `src/TestFramework/TestFramework.Extensions/AppModel.cs` — only mentions
  "WinUI source base" without a concrete upstream URL.
- `src/Polyfills/HashHelpers.cs` — the upstream reference is to the
  `dotnet/coreclr#1830` pull request (now merged into `dotnet/runtime`), not a
  specific file; the local code is a trivial three-line helper.
- `src/Platform/Microsoft.Testing.Extensions.TrxReport/Hashing/BitOperations.cs`
  and `EmbeddedAttribute.cs` — trivial compiler shims/polyfills that have not
  meaningfully changed upstream.
