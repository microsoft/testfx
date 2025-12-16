# Artifact Naming Service

The artifact naming service provides a standardized way to generate consistent names and paths for test artifacts across all extensions.

## Features

### Template-Based Naming

Use placeholders in angle brackets to create dynamic file names:

```
<pname>_<pid>_<id>_hang.dmp
```
Resolves to: `MyTests_12345_a1b2c3d4_hang.dmp`

### Complex Path Templates

Create structured directory layouts:

```
<root>/artifacts/<os>/<asm>/dumps/<pname>_<pid>_<tfm>_<time>.dmp
```
Resolves to: `c:/myproject/artifacts/linux/MyTests/dumps/my-child-process_10001_net9.0_2025-09-22T13:49:34.dmp`

### Available Placeholders

| Placeholder | Description | Example |
|-------------|-------------|---------|
| `<pname>` | Name of the process | `MyTests` |
| `<pid>` | Process ID | `12345` |
| `<id>` | Short random identifier (8 chars) | `a1b2c3d4` |
| `<os>` | Operating system | `windows`, `linux`, `macos` |
| `<asm>` | Assembly name | `MyTests` |
| `<tfm>` | Target framework moniker | `net9.0`, `net8.0` |
| `<time>` | Timestamp (1-second precision) | `2025-09-22T13:49:34` |
| `<root>` | Project root directory | Found via solution/git/working dir |

### Backward Compatibility

Legacy patterns are still supported:

```csharp
// Old pattern
"myfile_%p.dmp"

// Works with legacy support
service.ResolveTemplateWithLegacySupport("myfile_%p.dmp", 
    legacyReplacements: new Dictionary<string, string> { ["%p"] = "12345" });
```

### Custom Replacements

Override default values for specific scenarios:

```csharp
// When dumping a different process than the test host
var customReplacements = new Dictionary<string, string>
{
    ["pname"] = "Notepad",
    ["pid"] = "1111"
};

string result = service.ResolveTemplate("<pname>_<pid>.dmp", customReplacements);
// Result: "Notepad_1111.dmp"
```

## Usage in Extensions

Extensions can use the service through dependency injection:

```csharp
public class MyExtension
{
    private readonly IArtifactNamingService _artifactNamingService;
    
    public MyExtension(IServiceProvider serviceProvider)
    {
        _artifactNamingService = serviceProvider.GetArtifactNamingService();
    }
    
    public void CreateArtifact(string template)
    {
        string fileName = _artifactNamingService.ResolveTemplate(template);
        // Use fileName for artifact creation
    }
}
```

## Hang Dump Integration

The hang dump extension now uses the artifact naming service and supports both legacy and modern patterns:

```bash
# Legacy pattern (still works)
--hangdump-filename "mydump_%p.dmp"

# New template pattern  
--hangdump-filename "<pname>_<pid>_<id>_hang.dmp"

# Complex path template
--hangdump-filename "<root>/dumps/<os>/<pname>_<pid>_<time>.dmp"
```

This provides consistent artifact naming across all extensions while maintaining backward compatibility.
