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
| `eng/build-with-cache.ps1` | Convenience wrapper that runs a cached local build with the correct MSBuild engine and switches. |

The plugin's own `build/*.props` and `build/*.targets` are imported automatically by the
`GlobalPackageReference` when the package is restored.

## Requirements — read this before running

The plugin needs MSBuild's file-access sandbox, which has two hard requirements:

- **x64 desktop `MSBuild.exe`** (from Visual Studio 17.9+), specifically the x64 flavor at
  `MSBuild\Current\Bin\amd64\MSBuild.exe`.
  - The .NET (Core) MSBuild used by `dotnet build` / `dotnet msbuild` **does not** support the
    `-reportFileAccesses` switch and will fail with `MSB1001: Unknown switch`.
  - The 32-bit desktop MSBuild fails with *"Reporting file accesses is only currently supported using the
    x64 flavor of MSBuild."*
- The build must be invoked with `-graph -m -reportFileAccesses` and `/p:MSBuildCacheEnabled=true`.

## Running a cached build locally

Local builds use the `Microsoft.MSBuildCache.Local` backend (BuildXL `LocalCache`). The helper script
locates the x64 desktop MSBuild, points it at the repo-pinned SDK, and passes the required switches. Because
incremental builds are not supported, start from a clean tree:

```powershell
git clean -xdf
# Build a single project (fast to try) or omit -Project to build the whole solution.
.\eng\build-with-cache.ps1 -Project src\Analyzers\MSTest.Analyzers\MSTest.Analyzers.csproj -Configuration Release
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

In CI the `Microsoft.MSBuildCache.AzurePipelines` backend is selected automatically (it keys off the
`TF_BUILD` variable) and uses [Azure Pipeline Caching](https://learn.microsoft.com/azure/devops/pipelines/release/caching)
as the shared cache store. Three things are required:

1. Build with the **x64 desktop MSBuild** engine (Arcade: `-msbuildEngine vs`; ensure the x64 flavor is
   used) so `-reportFileAccesses` is available.
2. Pass `-graph -m -reportFileAccesses -p:MSBuildCacheEnabled=true` to the build step.
3. Expose the OAuth token and grant it the Pipeline Caching scope, because the plugin stores/reads cache
   content through Azure Pipeline Caching:

   ```yaml
   variables:
     # Grants $(System.AccessToken) the scope required by Pipeline Caching.
     EnablePipelineCache: true

   steps:
   - script: eng\common\CIBuild.cmd -configuration Release -msbuildEngine vs
       -graph -m -reportFileAccesses /p:MSBuildCacheEnabled=true
     env:
       SYSTEM_ACCESSTOKEN: $(System.AccessToken)
   ```

See [Pipeline caching – cache isolation and security](https://learn.microsoft.com/azure/devops/pipelines/release/caching#cache-isolation-and-security)
for how cache entries are scoped between branches/PRs.

> [!NOTE]
> Turning caching on in the repo's CI by default is intentionally left as a follow-up: it requires the
> Windows jobs to build with the x64 desktop MSBuild engine through Arcade. The wiring in this repo is
> validated and ready; flipping CI on is a separate, reviewed change.

## Troubleshooting

- **Cache misses that should be hits** — usually a global property that changes every build is part of the
  fingerprint (e.g. a version stamp). Add it to `MSBuildCacheGlobalPropertiesToIgnore` in
  `eng/projectcaching.props`. Inspect the logs under the `MSBuildCache` log directory to see the fingerprint
  inputs.
- **"file accessed after project finished" / duplicate output errors** — a detached process (telemetry,
  compiler server) touched a file after the project completed, or two projects wrote the same file. Add the
  path to `MSBuildCacheAllowFileAccessAfterProjectFinishFilePatterns` or
  `MSBuildCacheIdenticalDuplicateOutputPatterns` respectively.
- **`MSB1001: Unknown switch` for `-reportFileAccesses`** — you're using `dotnet build` / Core MSBuild. Use
  the x64 desktop `MSBuild.exe` instead (see Requirements).
- **Package restore fails for `Microsoft.MSBuildCache.*`** — the dnceng `dotnet-public` feed proxies
  nuget.org, so the packages resolve on first restore. If a locked-down environment blocks the upstream,
  the packages must be mirrored to `dotnet-public` (or nuget.org added to `nuget.config`).

The full list of settings is documented in the [MSBuildCache README](https://github.com/microsoft/MSBuildCache).
