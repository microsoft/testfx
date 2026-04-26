# Artifact Naming Service

The artifact naming service provides a standardized way to generate consistent names and paths for test artifacts across all extensions.

## Template-Based Naming

Use placeholders in angle brackets to create dynamic file names:

```text
<pname>_<pid>_<id>_hang.dmp
```

Resolves to: `MyTests_12345_a1b2c3d4_hang.dmp`

## Available Placeholders

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `<pname>` | Name of the process | `MyTests` |
| `<pid>` | Process ID | `12345` |
| `<id>` | Short random identifier (8 chars) | `a1b2c3d4` |
| `<os>` | Operating system | `windows`, `linux`, `macos` |
| `<asm>` | Assembly name | `MyTests` |
| `<tfm>` | Target framework moniker | `net9.0`, `net8.0` |
| `<time>` | Timestamp (1-second precision) | `2025-09-22T13-49-34` |

## Backward Compatibility

Legacy patterns like `%p` continue to work in the hang dump extension.

## Custom Replacements

Override default values for specific scenarios:

```csharp
var customReplacements = new Dictionary<string, string>
{
    ["pname"] = "Notepad",
    ["pid"] = "1111"
};

string result = service.ResolveTemplate("<pname>_<pid>.dmp", customReplacements);
// Result: "Notepad_1111.dmp"
```

## Hang Dump Integration

The hang dump extension uses the artifact naming service and supports both legacy and modern patterns:

```text
# Legacy pattern (still works)
--hangdump-filename "mydump_%p.dmp"

# New template pattern
--hangdump-filename "<pname>_<pid>_<id>_hang.dmp"
```
