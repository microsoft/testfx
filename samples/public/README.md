# Public samples

## Windows application testing

The sample name identifies the Windows application model, test host, and packaging mode.

| Application model | Test host | Packaging | Sample | Status |
| --- | --- | --- | --- | --- |
| UWP | VSTest | Packaged | [`UwpVSTestApp`](UwpVSTestApp) | Supported |
| UWP | Microsoft.Testing.Platform (MTP) | Packaged | — | Not supported; true UWP/AppContainer applications still require the VSTest host |
| WinUI 3 | VSTest | Packaged | [`WinUIVSTestApp`](WinUIVSTestApp) | Supported |
| WinUI 3 | VSTest | Unpackaged | — | Not supported; VSTest's WinUI provider requires an AppX manifest |
| WinUI 3 | MTP | Packaged | [`WinUIMtpPackagedApp`](WinUIMtpPackagedApp) | Supported |
| WinUI 3 | MTP | Unpackaged | [`WinUIMtpUnpackagedApp`](WinUIMtpUnpackagedApp) | Supported |

UWP is always packaged. For more detail about packaging, trust levels, launch behavior, and UI-thread
tests, see [Testing UWP and WinUI apps with MSTest](../../docs/winui-testing.md).

Run commands from a sample directory and select a concrete architecture:

```powershell
# MTP samples
dotnet build -p:Platform=x64 -bl:{{}}
dotnet run --no-build -p:Platform=x64
dotnet test --project . --no-build -p:Platform=x64

# UWP VSTest sample
dotnet test UwpVSTestApp.csproj -p:Platform=x64

# WinUI VSTest sample
dotnet test WinUIVSTestApp.csproj -p:Platform=x64
```

The VSTest samples require Visual Studio with the Universal Windows Platform workload and test tools.
