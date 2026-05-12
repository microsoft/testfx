# Artifact Naming Helper

The `ArtifactNamingHelper` is a shared static helper that provides a standardized way to generate consistent names and paths for test artifacts. It is compiled into each extension that needs it via file linking (no service registration or IVT required).

## Template-Based Naming

Use placeholders in curly braces to create dynamic file names. Placeholder matching is **case-sensitive** — use lowercase placeholder names (e.g., `{pname}`, not `{PName}`).

```text
{pname}_{pid}_{time}_hang.dmp
```

Resolves to: `MyTests_12345_2025-09-22_13-49-34.0000000_hang.dmp`

## Available Placeholders

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `{pname}` | Name of the process | `MyTests` |
| `{pid}` | Process ID | `12345` |
| `{os}` | Operating system | `windows`, `linux`, `macos`, `unknown` |
| `{asm}` | Assembly name | `MyTests` |
| `{tfm}` | Target framework moniker | `net9.0`, `net8.0` |
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
