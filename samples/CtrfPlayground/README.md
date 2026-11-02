# CtrfPlayground

This sample produces side-by-side [CTRF (Common Test Report Format)](https://ctrf.io/) reports
from the same set of tests using two different CTRF generators that both target
[Microsoft.Testing.Platform](https://aka.ms/testingplatform):

| Project | Test framework | CTRF generator |
| ------- | -------------- | -------------- |
| [`Mtp`](./Mtp) | MSTest | `Microsoft.Testing.Extensions.CtrfReport` (this repository) |
| [`XunitMtp`](./XunitMtp) | [`xunit.v3`](https://www.nuget.org/packages/xunit.v3) | `xunit.v3`'s built-in `-ctrf <file>` reporter |

The tests in each project are intentionally equivalent (pass / fail / skip / theory / throw)
so that the two CTRF reports can be diffed to validate the parity of the new extension.

## Running

From the repository root:

```pwsh
# MSTest + Microsoft.Testing.Extensions.CtrfReport
dotnet run --project samples/CtrfPlayground/Mtp -- --report-ctrf --results-directory ./out/mtp

# xunit.v3 with its built-in CTRF reporter
dotnet run --project samples/CtrfPlayground/XunitMtp -- -ctrf ./out/xunit/ctrf-report.json
```

Both runs write a CTRF JSON file. Open them side by side (e.g.
`code -d ./out/mtp/<file>.ctrf.json ./out/xunit/ctrf-report.json`) to compare the two outputs.

> [!NOTE]
> The stable CTRF producer shares MTP's package version and is pinned to CTRF 0.2.0. Framework UID stability and
> producer-specific diagnostic metadata retain the limitations in the
> [package compatibility policy](../../src/Platform/Microsoft.Testing.Extensions.CtrfReport/PACKAGE.md#compatibility).

<!-- markdownlint-disable-next-line MD028 -->
> [!TIP]
> The two projects are intentionally **kept separate**: combining a CTRF-producing
> extension with another test framework that already exposes its own CTRF reporter would
> conflict on command-line options inside a single test host.
