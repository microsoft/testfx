# Run an MSTest partition

Native MSTest applications on Microsoft.Testing.Platform can use
`--partition index/count` to select one numbered part of a test assembly.
This option requires an MSTest build that includes partition selection; it is
not a VSTest command-line option.

```powershell
# Select partition 2 of 4 using native MTP dotnet test.
dotnet test --project .\MyTests\MyTests.csproj --partition 2/4

# Or invoke the built native MSTest application directly.
.\MyTests.exe --partition 2/4
dotnet .\MyTests.dll --partition 2/4
```

For `dotnet test`, select the Microsoft.Testing.Platform runner in `global.json`
as described in the
[native MTP CLI documentation](https://learn.microsoft.com/dotnet/core/tools/dotnet-test-mtp).

## What the option does

`index` and `count` are positive decimal Int32 values using ASCII digits,
numbering partitions from `1` through `count`. Leading zeros are allowed.
For example, `2/4` runs only partition 2, not all four. `1/1` selects the same
work as an ordinary run. Signs and embedded whitespace are invalid; MTP's
ordinary outer-whitespace argument normalization is unchanged.

The option **does not start extra processes or enable in-process parallelism**.
To run partitions concurrently, configure separate CI jobs or use an external
launcher. Run every index with the same partition count, test binaries and
selection inputs.

```powershell
# Separate CI jobs run these commands against the same test build.
dotnet test --project .\MyTests\MyTests.csproj --partition 1/4
dotnet test --project .\MyTests\MyTests.csproj --partition 2/4
dotnet test --project .\MyTests\MyTests.csproj --partition 3/4
dotnet test --project .\MyTests\MyTests.csproj --partition 4/4
```

Give each invocation its own results directory when jobs share a filesystem:

```powershell
dotnet test --project .\MyTests\MyTests.csproj --partition 2/4 --results-directory .\TestResults\partition-2
```

## Selection and fixtures

Partition assignment is deterministic for the same module filename, concrete
class names, dependency topology and count. Checkout paths and test discovery
order do not change it. Changing the count, renaming a class/module, or changing
dependencies can move work between partitions.
Different modules with the same filename and class names can have correlated
assignments; each module is still partitioned independently.

A class and its data rows stay together, including folded and execution-time
data. Classes connected by resolvable declared dependencies stay in the same
partition. Ordinary filters still apply; partitioning never adds a filtered-out
prerequisite back into the run.

```powershell
dotnet test --project .\MyTests\MyTests.csproj --filter "TestCategory=Integration" --partition 2/4
dotnet test --project .\MyTests\MyTests.csproj --list-tests --partition 2/4
```

Discovery and execution retain their respective filter-provider behavior, so
listing tests is not proof of execution. A single large class or dependency
component cannot be split by this option. Hash assignment does not guarantee
equal test counts or durations.

Each invocation performs its normal fixture lifecycle for the selected tests.
Separate processes have separate static state, but still share external files,
databases and ports. `[DoNotParallelize]` and resource locks retain their
process-local meaning; they do not coordinate separate CI jobs.

## Retry and empty selections

When the MTP Retry extension is registered, it can retry failures inside the
selected partition:

```powershell
dotnet test --project .\MyTests\MyTests.csproj --partition 2/4 --retry-failed-tests 3
```

The partition stays fixed when retries narrow the request to failed test UIDs.
Retry thresholds apply to that invocation's selected work, not the assembly's
unfiltered tests or other CI jobs. MSTest's in-process retry behavior is
unchanged.

An empty partition follows the normal zero-test policy: exit code `8` by
default, or the normal explicit-minimum failure when one is configured.
Hashing can produce empty partitions even when the class count is larger than
the partition count. A zero-test result can also indicate an incorrect filter;
do not blindly ignore it as proof of a successful test run.

JSON-RPC/IDE server mode and WebAssembly are not supported by this first
implementation. See the [design and decisions](decisions/test-partitioning.md)
for the contract and validation evidence.
