# Artifact Naming Helper

> **Current implementation note (2026-10-09):** [ArtifactNamingHelper](../../src/Platform/Microsoft.Testing.Platform/Services/ArtifactNamingHelper.cs)
> now derives `{tfm}` from entry-assembly display/platform metadata via `TargetFrameworkParser`,
> falling back to runtime description when display metadata is unavailable. The service wrapper
> [ArtifactNamingService](../../src/Platform/Microsoft.Testing.Platform/Services/ArtifactNamingService.cs)
> also supplies module identity and sanitizes the leaf name.
> See the [MTP architecture](../architecture/mtp.md#reports-and-optional-integrations) for artifact ownership.

`ArtifactNamingHelper` is an internal static helper with two current consumption paths:

- Extensions can compile a private copy through source linking. For example, the
  [HangDump project](../../src/Platform/Microsoft.Testing.Extensions.HangDump/Microsoft.Testing.Extensions.HangDump.csproj)
  links the helper, and [dump writing](../../src/Platform/Microsoft.Testing.Extensions.HangDump/HangDumpProcessLifetimeHandler.DumpWriting.cs)
  calls it directly. This embedded path needs neither naming-service registration nor IVT.
- The core host [registers `ArtifactNamingService`](../../src/Platform/Microsoft.Testing.Platform/Hosts/TestHostBuilder.CommonServices.cs),
  which wraps the same helper. Extensions can access this registered public contract through
  [`GetArtifactNamingService()`](../../src/Platform/Microsoft.Testing.Platform/Services/ServiceProviderExtensions.cs)
  and `IArtifactNamingService.ResolveFileName`; source linking is not required for this path.

The static-helper examples below describe internal/source-linked usage, not a public helper API.

## Template-Based Naming

Use placeholders in curly braces to create dynamic file names. Placeholder matching is **case-sensitive** — use lowercase placeholder names (e.g., `{pname}`, not `{PName}`).

```text
{pname}_{pid}_{time}_hang.dmp
```

Resolves to: `MyTests_12345_2025-09-22_13-49-34.0000000_hang.dmp`

## Available Placeholders

| Placeholder | Description | Example |
| --- | --- | --- |
| `{pname}` | Name of the process | `MyTests` |
| `{pid}` | Process ID | `12345` |
| `{asm}` | Assembly name (entry assembly, or `unknown` if unavailable) | `MyTests` |
| `{tfm}` | Target framework moniker from entry-assembly display/platform metadata; falls back to runtime description when display metadata is absent/blank, then `unknown` if unresolved | `net9.0`, `net8.0-windows10.0.18362.0` |
| `{arch}` | Process architecture (lowercased value of `RuntimeInformation.ProcessArchitecture`) | `x64`, `x86`, `arm64` |
| `{time}` | Timestamp (high precision) | `2025-09-22_13-49-34.0000000` |

## Backward Compatibility

Legacy patterns like `%p` continue to work in the hang dump extension.

## Custom Replacements

Override default values for specific scenarios:

```csharp
var replacements = new Dictionary<string, string>
{
    ["pname"] = "Notepad",
    ["pid"] = "1111"
};

string result = ArtifactNamingHelper.ResolveTemplate("{pname}_{pid}.dmp", replacements);
// Result: "Notepad_1111.dmp"
```

## Hang Dump Integration

The hang dump extension uses the artifact naming helper and supports both legacy and modern patterns:

```text
# Legacy pattern (still works)
--hangdump-filename "mydump_%p.dmp"

# New template pattern
--hangdump-filename "{pname}_{pid}_{time}_hang.dmp"
```
