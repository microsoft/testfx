# Early access to MSTest packages

Stable versions (and selected previews) of MSTest, and related packages, are distributed through [nuget.org](https://www.nuget.org/packages/MSTest)

We also publish every successful merge to main and release branches to our preview nuget channel test-tools.

To use this channel it needs to be added to your configuration, typically by creating [NuGet.Config](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file) file with the following content. And placing it next to your solution file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!-- MSTest early access packages. See: aka.ms/mstest/preview -->
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

Adding additional nuget feeds might lead to warnings or errors from build systems that check compliance. This is because using multiple public and private sources might lead to possible dependency confusion attacks. All the packages we publish to nuget.org are using a reserved prefix. But this might not mitigate the risk in your setup. If this is a concern to you, please discuss with your internal security department.

### Usage with central package management

Solutions that use central package management through `Directory.Packages.props` will see `NU1507` warnings about multiple package sources. To solve this add this section to your NuGet.Config file:

```xml
<packageSourceMapping>
  <!-- key value for <packageSource> should match key values from <packageSources> element -->
  <packageSource key="nuget.org">
    <package pattern="*" />
  </packageSource>
  <packageSource key="test-tools">
    <package pattern="MSTest.*" />
    <package pattern="Microsoft.Testing.*" />
  </packageSource>
</packageSourceMapping>
```

Full documentation of package source mapping can be [found here](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping#enable-by-manually-editing-nugetconfig).