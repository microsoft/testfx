# Early access to MSTest runner packages

Stable versions (and selected previews) of MSTest runner, and related packages, are distributed through [nuget.org](https://www.nuget.org/packages/MSTest)

We also publish every successful merge to main and release branches to our preview nuget channel test-tools.

To use this channel it needs to be added to your configuration, typically by creating [NuGet.Config](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file) file with the following content. And placing it next to your solution file: 

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="test-tools"
        value="https://pkgs.dev.azure.com/dnceng/public/_packaging/test-tools/nuget/v3/index.json" />
  </packageSources>
</configuration>
```

## NuGet.Config placement

NuGet.Config file can be placed next to solution file, or next to project file when you don't have solution file. But in cases where you have solution file, you should always place it next to solution file, to ensure consitent behavior in Visual Studio and in command line.


## Selecting a version

Test-tools feed can be [browsed interactively](https://dev.azure.com/dnceng/public/_artifacts/feed/test-tools/NuGet/MSTest/versions/).

## Warranty

Packages from test-tools feed are considered experimental, might not have the usual quality, and come without warranty.

## Dependency confusion attack

Adding additional nuget feeds might lead to warnings or errors from build systems that check compliance. This is because using multiple public and private sources might lead to possible dependency confusion attacks. We guard against this type of attack by reserving our package prefixes on Nuget.org, but compliance systems might just check if count of feeds is more than 1. If this is a concern to you, please discuss with your internal security department.


