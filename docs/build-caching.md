# Build caching with BuildXL (MSBuildCache)

testfx can build with the [BuildXL](https://github.com/microsoft/BuildXL)-backed
[MSBuildCache](https://github.com/microsoft/MSBuildCache) MSBuild project-cache plugin. When enabled, the
plugin caches the outputs of each project — keyed on the exact set of files the project reads and the
compiler/tool inputs — so that unchanged projects are served from the cache instead of being rebuilt.
Cached outputs can be shared across machines (Azure Pipelines) or reused locally.

> [!IMPORTANT]
> MSBuildCache only produces correct results on a **clean repo** (a fresh checkout, a PR build, or a CI
> build). It does **not** support incremental local dev builds where `bin`/`obj` from a previous build are
> still around. Use it on a clean tree, or run `git clean -xdf` first.

## How it is wired up

Caching is **off by default**. Nothing is restored or imported unless you pass
`/p:MSBuildCacheEnabled=true`, so normal `build.cmd` / `build.sh` runs are unaffected.

| File | Role |
| --- | --- |
| `Directory.Packages.props` | Selects the backend package (`Microsoft.MSBuildCache.AzurePipelines` in Azure Pipelines, `Microsoft.MSBuildCache.Local` elsewhere), pins `MSBuildCachePackageVersion`, and adds the `GlobalPackageReference`s — all guarded by `MSBuildCacheEnabled`. |
| `eng/projectcaching.props` | Repo-specific plugin settings (cache universe, allowed post-project file accesses). Imported from `Directory.Build.props` only when caching is enabled. |
| `Directory.Build.props` | Conditionally imports `eng/projectcaching.props`. |
| `eng/build-with-cache.ps1` | Convenience wrapper that runs a cached build with the correct MSBuild engine and switches. Used both locally and by the Windows CI job. |
| `azure-pipelines.yml` | The Windows job builds under the cache (Restore → Build-with-cache → Pack). |

The plugin's own `build/*.props` and `build/*.targets` are imported automatically by the
`GlobalPackageReference` when the package is restored.

> [!NOTE]
> The plugin is registered from the evaluation of the graph **entry** project(s), so it only caches when
> the real project/solution graph is the MSBuild entry point. Building through Arcade's `Build.proj` (which
> builds projects via a nested MSBuild task) does **not** engage the plugin — hence the direct
> `MSBuild.exe TestFx.slnx` build used by the helper and CI.

## Requirements — read this before running

The plugin needs MSBuild's file-access sandbox, which has two hard requirements:

- **x64 desktop `MSBuild.exe`**, specifically the x64 flavor at `MSBuild\Current\Bin\amd64\MSBuild.exe`.
  - The .NET (Core) MSBuild used by `dotnet build` / `dotnet msbuild` **does not** support the
    `-reportFileAccesses` switch and will fail with `MSB1001: Unknown switch`.
  - The 32-bit desktop MSBuild fails with *"Reporting file accesses is only currently supported using the
    x64 flavor of MSBuild."*
- The build must be invoked with `-graph -m -reportFileAccesses` and `/p:MSBuildCacheEnabled=true`.

Two different Visual Studio floors apply, and the higher one wins:

- The MSBuildCache **plugin** itself requires VS **17.9+** (for `-reportFileAccesses`).
- **testfx** pins its own, higher requirement: `global.json` requires the VS version declared there (today
  17.14.x with the Universal workload), and the default `TestFx.slnx` requires VS 2022 **17.13+** (see
  `docs/dev-guide.md`). So building the default solution needs the testfx-pinned VS, not merely 17.9.

The `eng/build-with-cache.ps1` helper does **not** hand-pick a VS install: it uses the Arcade helper
(`InitializeVisualStudioMSBuild`), which enforces the `global.json` requirement and selects the
host-architecture MSBuild, then the helper additionally asserts the resolved MSBuild is the required x64
flavor. Only reach for a lower (17.9-17.12) VS when building an individual project that does not need the
full solution's newer VS.

## Running a cached build locally

Local builds use the `Microsoft.MSBuildCache.Local` backend (BuildXL `LocalCache`). The helper script
bootstraps the repo-pinned .NET SDK, locates the x64 desktop MSBuild that satisfies `global.json`, and
passes the required switches. Because incremental builds are not supported, start from a clean tree.

> [!NOTE]
> `git clean -xdf` also deletes the repo-local `.dotnet` SDK. The helper re-bootstraps it (via the Arcade
> `InitializeDotNetCli` helper) on its next run, so the sequence below works; but if you invoke MSBuild
> yourself instead of the helper, run `.\build.cmd -restore` (or `.\eng\common\build.ps1 -restore`) after
> the clean to reinstall the pinned SDK first.

```powershell
git clean -xdf
# Build a single project (fast to try) or omit -Project to build the whole solution.
.\eng\build-with-cache.ps1 -Project src\Analyzers\MSTest.Analyzers\MSTest.Analyzers.csproj -configuration Release
```

Run it, then run it again after removing the outputs (or another `git clean -xdf`). The first run populates
the cache; the second reports cache hits, e.g.:

```text
Loading the following project cache plugin: MSBuildCacheLocalPlugin
MSTest.Analyzers -> Cache Hit
Project cache statistics:
  Cache Hit Count: 1 (saved 11.4 project-seconds)
  Cache Hit Ratio: 100.0%
```

To bust the whole local cache, pass `-CacheUniverse <new-value>` (or bump `MSBuildCacheCacheUniverse` in
`eng/projectcaching.props`), or delete the local cache directory (defaults to `\MSBuildCache` at the drive
root).

If you prefer to invoke MSBuild yourself:

```powershell
& "<VS>\MSBuild\Current\Bin\amd64\MSBuild.exe" TestFx.slnx `
    -graph -m -reportFileAccesses -restore `
    -p:Configuration=Release -p:MSBuildCacheEnabled=true `
    -p:MSBuildCacheLogDirectory=artifacts\log\Release\MSBuildCache
```

## Running a cached build in Azure Pipelines

Caching is **enabled by default on the non-fork Windows CI job**. Because the plugin only engages when the
real project graph is the MSBuild entry point (not through Arcade's `Build.proj`, which builds projects via a
nested MSBuild task), the Windows job is split into three steps:

1. **Restore (Arcade, cache)** — `eng\common\CIBuild.cmd ... /p:Build=false /p:Pack=false /p:Sign=false
   /p:MSBuildCacheEnabled=true` restores the toolset and all packages. Passing `MSBuildCacheEnabled=true`
   here is required: it makes restore include the plugin's `GlobalPackageReference` so the next (`-NoRestore`)
   step actually imports the plugin.
2. **Build (BuildXL cache)** — `eng\build-with-cache.ps1 -ci` runs the x64 desktop MSBuild on `TestFx.slnx`
   with `-graph -m -reportFileAccesses -p:MSBuildCacheEnabled=true`, plus the CI build semantics Arcade would
   otherwise apply (`ContinuousIntegrationBuild=true`, `-warnaserror`, `-nr:false`). The
   `Microsoft.MSBuildCache.AzurePipelines` backend is selected automatically (via `TF_BUILD`) and uses
   [Azure Pipeline Caching](https://learn.microsoft.com/azure/devops/pipelines/release/caching) as the
   shared cache store. This step maps `SYSTEM_ACCESSTOKEN`, and the job sets the `EnablePipelineCache`
   variable to grant that token the Pipeline Caching scope.
3. **Sign + Pack (Arcade)** — `eng\common\CIBuild.cmd ... /p:Build=false` signs and packs the already-built
   outputs.

**Fork PRs** do not receive `SYSTEM_ACCESSTOKEN`, which the Azure Pipelines cache backend requires. The
three cached steps are therefore gated on `ne(variables._IsFork, 'True')`, and a fork build instead runs a
single uncached Arcade build (the pre-caching behaviour), so fork PRs stay green.

See [Pipeline caching – cache isolation and security](https://learn.microsoft.com/azure/devops/pipelines/release/caching#cache-isolation-and-security)
for how cache entries are scoped between branches/PRs.

> [!IMPORTANT]
> This is a **first-cut, gating** integration. A full-repo cached build under the file-access sandbox
> surfaces per-project issues (files written after a project finishes, non-deterministic outputs, duplicate
> writes across projects, cache-busting global properties) that can only be shaken out by real CI runs.
> Expect to add exclusions to `eng/projectcaching.props` (e.g.
> `MSBuildCacheIgnoredOutputPatterns`, `MSBuildCacheAllowFileAccessAfterProjectFinishFilePatterns`,
> `MSBuildCacheIdenticalDuplicateOutputPatterns`, `MSBuildCacheGlobalPropertiesToIgnore`) over a few
> iterations before the Windows job is reliably green. The `BuildCache.binlog` published under
> `artifacts/log/<Configuration>` and the `MSBuildCache` log directory are the primary debugging inputs.

## Troubleshooting

- **Cache misses that should be hits** — a global property that changes every build is part of the
  fingerprint. It is only safe to add a property to `MSBuildCacheGlobalPropertiesToIgnore` if it provably
  does **not** affect any output. In particular, do **not** ignore a version/build-number stamp: it is baked
  into assemblies and packages, so ignoring it would let the cache replay artifacts carrying a stale version.
  For genuinely output-irrelevant properties (e.g. a machine name or a timestamp used only for logging),
  ignoring them is appropriate. Inspect the logs under the `MSBuildCache` log directory to see exactly which
  fingerprint input changed before deciding.
- **"file accessed after project finished"** — a detached process (e.g. telemetry) touched a file after the
  project completed. Only add the path to `MSBuildCacheAllowFileAccessAfterProjectFinishFilePatterns` if that
  access provably does not feed the project's outputs; allow-listed accesses are excluded from cache
  accounting, so allow-listing a real build input would create invalid cache hits. Do **not** allow-list
  compiler/tool inputs — the `Microsoft.MSBuildCache.SharedCompilation` package (already referenced) exists
  to keep the Roslyn compiler-server accesses tracked.
- **Duplicate output errors** — two projects wrote the same file. If (and only if) the contents are
  identical and portable, add the path to `MSBuildCacheIdenticalDuplicateOutputPatterns`; otherwise fix the
  projects so they don't both produce it.
- **`MSB1001: Unknown switch` for `-reportFileAccesses`** — you're using `dotnet build` / Core MSBuild. Use
  the x64 desktop `MSBuild.exe` instead (see Requirements).
- **Package restore fails for `Microsoft.MSBuildCache.*`** — the dnceng `dotnet-public` feed proxies
  nuget.org, so the packages resolve on first restore. If a locked-down environment blocks the upstream,
  the packages must be mirrored to `dotnet-public` (or nuget.org added to `nuget.config`).

The full list of settings is documented in the [MSBuildCache README](https://github.com/microsoft/MSBuildCache).
