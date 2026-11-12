#requires -Version 7.2
<#
.SYNOPSIS
Exercises the coverage writer and self-contained WinUI build targets without restore or compilation of product assemblies.
.EXAMPLE
.\eng\test-build-incrementality.ps1 -ArtifactsDirectory C:\scratch\incrementality -PackageRoot Q:\.tools\.nuget\packages
.EXAMPLE
.\eng\test-build-incrementality.ps1 -ArtifactsDirectory C:\scratch\incrementality -TestName CoverageSettingsPreserveExactBytesAndTimestamp -CoverageTargetsPath C:\scratch\baseline-test.Directory.Build.targets
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$CoverageTargetsPath,
    [string]$WinUITargetsPath,
    [string]$PackageRoot = $env:NUGET_PACKAGES,
    [string]$ManifestTool,
    [string]$ArtifactsDirectory,
    [string[]]$TestName = @("*"),
    [switch]$ChildBuild,
    [string]$BuildRequest
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Bootstrap in a child only: tools.ps1 changes PATH, TEMP, and other process-wide settings.
if ($ChildBuild) {
    $request = [IO.File]::ReadAllText($BuildRequest) | ConvertFrom-Json
    $restore = $false
    $prepareMachine = $false
    $disableConfigureToolsetImport = $true
    . (Join-Path $request.RepositoryRoot "eng\common\tools.ps1")
    $dotnetRoot = InitializeDotNetCli -install:$false -createSdkLocationFile:$false
    $env:TEMP = $request.TempDirectory
    $env:TMP = $request.TempDirectory
    $env:MSBUILDDEBUGPATH = $request.TempDirectory
    $dotnetExecutable = [IO.Path]::GetFullPath((Join-Path $dotnetRoot "dotnet.exe"))
    $versionOutput = @(& $dotnetExecutable --version)
    $versionExitCode = $LASTEXITCODE
    $sdkVersion = ($versionOutput -join [Environment]::NewLine).Trim()
    if ($versionExitCode -ne 0 -or $sdkVersion -cne $GlobalJson.sdk.version) {
        throw "Expected SDK '$($GlobalJson.sdk.version)', got '$sdkVersion' from '$dotnetExecutable' (exit $versionExitCode)."
    }
    [IO.File]::WriteAllText($request.ToolchainFile, (ConvertTo-Json -InputObject @{
        Executable = $dotnetExecutable
        SDKVersion = $sdkVersion
    }), [Text.UTF8Encoding]::new($false))
    & $dotnetExecutable @($request.Arguments)
    exit $LASTEXITCODE
}

function Assert-TestCondition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "ASSERTION: $Message" }
}

function Assert-Equal {
    param([object]$Expected, [object]$Actual, [string]$Message)
    Assert-TestCondition ($Expected -ceq $Actual) "$Message`nExpected: $Expected`nActual:   $Actual"
}

function ConvertTo-XmlText {
    param([string]$Text)
    return [Security.SecurityElement]::Escape($Text)
}

function Write-FixtureFile {
    param([string]$Path, [string]$Text)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Get-BomBytes {
    param([string]$Text)
    $encoding = [Text.UTF8Encoding]::new($true)
    return ,([byte[]]($encoding.GetPreamble() + $encoding.GetBytes($Text)))
}

function Assert-BytesEqual {
    param([byte[]]$Expected, [byte[]]$Actual, [string]$Message)
    Assert-TestCondition ([Convert]::ToBase64String($Expected) -ceq [Convert]::ToBase64String($Actual)) (
        "$Message`nExpected ($($Expected.Length) bytes): $([Text.Encoding]::UTF8.GetString($Expected))" +
        "`nActual ($($Actual.Length) bytes): $([Text.Encoding]::UTF8.GetString($Actual))")
}

function Invoke-FixtureBuild {
    param(
        [string]$Project,
        [string]$Target,
        [string]$Label,
        [string[]]$Properties = @(),
        [switch]$MultiThreaded,
        [switch]$StrictTaskHost
    )

    $id = "$Label-$([guid]::NewGuid().ToString('N'))"
    $logRoot = Join-Path $script:CaseDirectory "logs"
    # -mt adds its own long sentinel-CWD suffix. Keep this GUID root directly
    # under the supplied scratch directory, not below the result/case/log paths.
    $temp = Join-Path $script:ScratchDirectory ([guid]::NewGuid().ToString("N"))
    [IO.Directory]::CreateDirectory($logRoot) | Out-Null
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    $binlog = Join-Path $logRoot "$id.binlog"
    $arguments = @("msbuild", $Project, "-nologo", "-nr:false", "-v:diag",
        "-p:UseSharedCompilation=false", "-t:$Target", "-bl:$binlog;ProjectImports=Embed")
    if ($MultiThreaded) { $arguments += @("-m:4", "-mt") }
    foreach ($property in $Properties) { $arguments += "-p:$property" }
    $requestPath = Join-Path $logRoot "$id.request.json"
    $toolchainPath = Join-Path $logRoot "$id.toolchain.json"
    Write-FixtureFile $requestPath (ConvertTo-Json -Depth 8 -InputObject @{
        RepositoryRoot = $script:RepositoryRoot
        TempDirectory = $temp
        ToolchainFile = $toolchainPath
        Arguments = $arguments
    })

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Process -Id $PID).Path
    foreach ($argument in @("-NoProfile", "-NonInteractive", "-File", $PSCommandPath,
        "-ChildBuild", "-BuildRequest", $requestPath)) {
        $start.ArgumentList.Add($argument)
    }
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = $script:WorkingDirectory
    $start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    $start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
    $start.Environment["DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK"] = "1"
    $start.Environment["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "true"
    $start.Environment["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "0"
    $start.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1"
    $start.Environment["MSBUILDDISABLENODEREUSE"] = "1"
    $start.Environment["TEMP"] = $temp
    $start.Environment["TMP"] = $temp
    $start.Environment["MSBUILDDEBUGPATH"] = $temp
    foreach ($name in @("MSBuildSDKsPath", "MSBUILD_EXE_PATH", "MSBUILDTASKHOSTLOCATION")) {
        $null = $start.Environment.Remove($name)
    }
    if ($StrictTaskHost) {
        # A marked coverage task must not need a task-host executable, even with -mt.
        $start.Environment["MSBUILDTASKHOSTLOCATION"] = Join-Path $temp "unavailable-task-host"
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $timedOut = $false
    $started = $false
    try {
        $null = $process.Start()
        $started = $true
        $processId = $process.Id
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $timedOut = $true
            $process.Kill($true)
        }
        $process.WaitForExit()
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $exitCode = $process.ExitCode
    } finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        $process.Dispose()
    }

    $stdoutPath = Join-Path $logRoot "$id.stdout.log"
    $stderrPath = Join-Path $logRoot "$id.stderr.log"
    Write-FixtureFile $stdoutPath $stdout
    Write-FixtureFile $stderrPath $stderr
    $toolchain = $null
    if ([IO.File]::Exists($toolchainPath)) {
        $toolchain = [IO.File]::ReadAllText($toolchainPath) | ConvertFrom-Json
    }
    $invocation = [pscustomobject]@{
        Test = $script:CaseName
        Label = $Label
        Executable = if ($null -ne $toolchain) { $toolchain.Executable } else { $null }
        SDKVersion = if ($null -ne $toolchain) { $toolchain.SDKVersion } else { $null }
        ToolchainFile = $toolchainPath
        Arguments = $arguments
        WorkingDirectory = $script:WorkingDirectory
        ProcessId = $processId
        ExitCode = $exitCode
        TimedOut = $timedOut
        StrictTaskHost = [bool]$StrictTaskHost
        TempDirectory = $temp
        Binlog = $binlog
        StandardOutput = $stdoutPath
        StandardError = $stderrPath
    }
    $script:Invocations.Add($invocation)
    if ($timedOut -or $exitCode -ne 0) {
        throw "BUILD: '$Label' exited $exitCode (timeout: $timedOut). See '$stdoutPath' and '$stderrPath'."
    }
    if ($null -eq $toolchain -or [string]::IsNullOrWhiteSpace($toolchain.Executable)) {
        throw "BUILD: '$Label' exited 0 without an actual selected executable in '$toolchainPath'."
    }
    if ($toolchain.SDKVersion -cne $script:SDKVersion -or -not [IO.Path]::IsPathRooted($toolchain.Executable) -or -not [IO.File]::Exists($toolchain.Executable)) {
        throw "BUILD: '$Label' has an invalid selected SDK/executable in '$toolchainPath'."
    }
    if (-not [IO.File]::Exists($binlog) -or ([IO.FileInfo]$binlog).Length -eq 0) {
        throw "BUILD: '$Label' did not produce its unique nonempty binlog '$binlog'."
    }
    return $stdout
}

function New-CoverageFixture {
    param([string]$Name = "Coverage", [string]$Module = "Alpha.Core", [bool]$Linked = $false)

    $root = Join-Path $script:CaseDirectory "$Name space &+[x](y)"
    $root = [IO.Path]::GetFullPath($root)
    $projectDirectory = Join-Path $root "projects\$Name"
    $project = Join-Path $projectDirectory "$Name.proj"
    $packages = "$root\packages (v1.0)+[deps]&cache\"
    $artifacts = "$root\artifacts\"
    Write-FixtureFile (Join-Path $root "Directory.Build.targets") @'
<Project>
  <PropertyGroup>
    <RepoRoot>$(MSBuildThisFileDirectory)</RepoRoot>
  </PropertyGroup>
</Project>
'@
    $targetCopy = Join-Path $root "test\Directory.Build.targets"
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($targetCopy)) | Out-Null
    [IO.File]::Copy($script:CoverageSnapshot, $targetCopy)
    Write-FixtureFile (Join-Path $root "ImportedReferences.targets") @'
<Project>
  <ItemGroup>
    <ProjectReference Include="$(RepoRoot)src\Transitive.NotDirect.csproj" />
  </ItemGroup>
</Project>
'@
    $text = @'
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Configuration>Debug</Configuration>
    <PlatformTarget>x64</PlatformTarget>
    <EnableCodeCoverage>true</EnableCodeCoverage>
    <EnableMSTestRunner>false</EnableMSTestRunner>
    <UseInternalTestFramework>false</UseInternalTestFramework>
    <IntermediateOutputPath>obj\</IntermediateOutputPath>
    <CodeCoverageSettingsFile>obj\coverage.config</CodeCoverageSettingsFile>
    <CodeCoverageIncludeSourceLinkedProductionCode>@LINKED@</CodeCoverageIncludeSourceLinkedProductionCode>
    <AssemblyName>@NAME@.LinkedTests</AssemblyName>
    <TargetExt>.exe</TargetExt>
    <FixtureModule>@MODULE@</FixtureModule>
    <NuGetPackageRoot>@PACKAGES@</NuGetPackageRoot>
    <ArtifactsDir>@ARTIFACTS@</ArtifactsDir>
    <RepoRoot>@ROOT@\</RepoRoot>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$(RepoRoot)src\$(FixtureModule).csproj" />
    <ProjectReference Include="$(RepoRoot)src\TestFramework.csproj" />
    <ProjectReference Include="$(RepoRoot)src\TestFramework.Extensions.csproj" />
    <ProjectReference Include="$(RepoRoot)src\Infrastructure.csproj" IsTestInfrastructure="true" />
    <ProjectReference Include="$(RepoRoot)test\Other.Tests.csproj" />
  </ItemGroup>
  <Import Project="$(RepoRoot)ImportedReferences.targets" />
  <Import Project="$(RepoRoot)test\Directory.Build.targets" />
  <Target Name="WriteMetadataProbe">
    <ItemGroup>
      <ProbeLine Include="not/the/value/one" Value="&lt;probe&gt;" />
      <ProbeLine Include="not\the\value\two">
        <Value>  &lt;path&gt;^[\\/]root[a-z]+\.(dll|exe)$ /_/src/ &amp;amp; tail&lt;/path&gt;</Value>
      </ProbeLine>
      <ProbeLine Include="not/the/value/three" Value="&lt;/probe&gt;" />
    </ItemGroup>
    <WriteCodeCoverageSettings FileName="$(MSBuildProjectDirectory)\probe.config" Lines="@(ProbeLine)" />
  </Target>
</Project>
'@
    foreach ($replacement in @{
        "@ROOT@" = $root; "@PACKAGES@" = $packages; "@ARTIFACTS@" = $artifacts
        "@NAME@" = $Name; "@MODULE@" = $Module; "@LINKED@" = $Linked.ToString().ToLowerInvariant()
    }.GetEnumerator()) {
        $text = $text.Replace($replacement.Key, (ConvertTo-XmlText $replacement.Value))
    }
    Write-FixtureFile $project $text
    return [pscustomobject]@{
        Root = $root; Project = $project; Directory = $projectDirectory
        Output = Join-Path $projectDirectory "obj\coverage.config"
        Packages = $packages; Artifacts = $artifacts; Module = $Module
        Linked = $Linked; AssemblyName = "$Name.LinkedTests"
    }
}

function Get-ExpectedCoverageBytes {
    param([object]$Fixture, [string]$Module, [bool]$Linked, [string]$Packages, [string]$Artifacts)

    $lines = [Collections.Generic.List[string]]::new()
    # Pin the original serialized bytes, including MSBuild's trimming of single XML
    # elements supplied through attribute metadata (element metadata retains indentation).
    $lines.AddRange([string[]]@('<?xml version="1.0" encoding="utf-8"?>',
        '<Configuration>', '<DoesNotReturnAttribute>None</DoesNotReturnAttribute>'))
    if ($Linked) { $lines.Add('<IncludeTestAssembly>True</IncludeTestAssembly>') }
    $lines.AddRange([string[]]@('  <CodeCoverage>', '    <Attributes mergeDefaults="False">',
        '<Exclude />', '    </Attributes>', '    <ModulePaths mergeDefaults="False">', '      <Include>'))
    foreach ($assembly in @($Module, "MSTest.TestFramework", "MSTest.TestFramework.Extensions")) {
        $pattern = [regex]::Escape($assembly)
        $lines.Add("        <ModulePath>.*$pattern\.dll`$</ModulePath>")
    }
    if ($Linked) {
        $pattern = [regex]::Escape($Fixture.AssemblyName)
        $lines.Add("        <ModulePath>.*$pattern\.exe`$</ModulePath>")
    }
    $lines.AddRange([string[]]@('      </Include>', '      <Exclude>',
        '<ModulePath>.*Microsoft\.Testing\.TestInfrastructure\.dll$</ModulePath>',
        '      </Exclude>', '    </ModulePaths>', '    <Sources>'))
    if ($Linked) {
        $pattern = ConvertTo-XmlText ("^" + [regex]::Escape("$($Fixture.Root)\src\") + ".*")
        $lines.AddRange([string[]]@('      <Include>', "        <Source>$pattern</Source>",
            '        <Source>^[\\/]_[\\/]src[\\/].*</Source>', '      </Include>'))
    }
    $lines.Add('      <Exclude>')
    foreach ($path in @($Packages, $Artifacts, "$($Fixture.Root)\test\Utilities\Microsoft.Testing.TestInfrastructure\")) {
        $pattern = ConvertTo-XmlText ("^" + [regex]::Escape($path) + ".*")
        $lines.Add("        <Source>$pattern</Source>")
    }
    $lines.AddRange([string[]]@('      </Exclude>', '    </Sources>',
        '<UseVerifiableInstrumentation>False</UseVerifiableInstrumentation>',
        '<EnableStaticNativeInstrumentation>False</EnableStaticNativeInstrumentation>',
        '<EnableDynamicNativeInstrumentation>False</EnableDynamicNativeInstrumentation>',
        '<EnableStaticManagedInstrumentation>False</EnableStaticManagedInstrumentation>',
        '<EnableDynamicManagedInstrumentation>True</EnableDynamicManagedInstrumentation>',
        '  </CodeCoverage>', '</Configuration>'))
    return ,(Get-BomBytes (($lines -join [Environment]::NewLine) + [Environment]::NewLine))
}

function Assert-Coverage {
    param([object]$Fixture)
    Assert-TestCondition ([IO.File]::Exists($Fixture.Output)) "Coverage output must be project-relative: '$($Fixture.Output)'."
    $expected = Get-ExpectedCoverageBytes $Fixture $Fixture.Module $Fixture.Linked $Fixture.Packages $Fixture.Artifacts
    Assert-BytesEqual $expected ([IO.File]::ReadAllBytes($Fixture.Output)) "Coverage must preserve the complete BOM, XML, newlines, regex text, and item Value metadata."
}

function Write-ComponentFragment {
    param([string]$Path, [string]$ClassName, [string]$Dll = "Fixture.Activation.dll", [switch]$Proxy)
    $proxyXml = if ($Proxy) {
        @'
    <Extension Category="windows.activatableClass.proxyStub">
      <ProxyStub ClassId="11111111-2222-3333-4444-555555555555">
        <Path>Fixture.Proxy.dll</Path>
        <Interface Name="Fixture.IProxy" InterfaceId="66666666-7777-8888-9999-000000000000" />
      </ProxyStub>
    </Extension>
'@
    } else { "" }
    Write-FixtureFile $Path @"
<Fragment xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
  <Extensions>
    <Extension Category="windows.activatableClass.inProcessServer">
      <InProcessServer>
        <Path>$Dll</Path>
        <ActivatableClass ActivatableClassId="$ClassName" ThreadingModel="both" />
      </InProcessServer>
    </Extension>
$proxyXml
  </Extensions>
</Fragment>
"@
}

function New-WinUIFixture {
    $directory = Join-Path $script:CaseDirectory "w"
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $upstream = Join-Path $directory "u.targets"
    $override = Join-Path $directory "o.targets"
    [IO.File]::Copy($script:UpstreamSnapshot, $upstream)
    [IO.File]::Copy($script:WinUISnapshot, $override)
    $import = Join-Path $directory "i.targets"
    Write-FixtureFile $import "<Project />"
    $component = Join-Path $directory "a"
    $native = Join-Path $component "runtimes-framework\win-x64\native"
    $fragment = Join-Path $component "runtimes-framework\package.appxfragment"
    Write-ComponentFragment $fragment "Fixture.Alpha" -Proxy
    $extraFragment = Join-Path $directory "b\runtimes-framework\package.appxfragment"
    Write-ComponentFragment $extraFragment "Fixture.Beta" "Fixture.Beta.dll"

    $payload = @(
        "Fixture.Activation.dll", "Fixture.Proxy.dll", "plain\Helper.dll", "Excluded.dll",
        "workloads.fixture.json", "restartAgent.exe", "map.html", "en-US\Strings.mui",
        "Assets\icon.png", "schema\Native.winmd", "views\Page.xaml", "views\Page.xbf",
        "Assets\resources.pri", "ignored.txt", "ignored.exe"
    )
    foreach ($relative in $payload) {
        Write-FixtureFile (Join-Path $native $relative) "Inert fixture payload: $relative"
    }
    foreach ($relative in @("metadata\Root.winmd", "metadata\10.0.17763.0\Older.winmd", "metadata\10.0.18362.0\Current.winmd")) {
        Write-FixtureFile (Join-Path $component $relative) "Inert fixture metadata: $relative"
    }
    $extraDll = Join-Path $directory "b\runtimes-framework\win-x64\native\Fixture.Beta.dll"
    Write-FixtureFile $extraDll "Inert extra component DLL"
    Write-FixtureFile (Join-Path $directory "existing.txt") "Existing project item"
    $userManifest = Join-Path $directory "user.manifest"
    Write-FixtureFile $userManifest @'
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity name="Fixture.Application" version="1.2.3.4" type="win32" processorArchitecture="amd64" />
  <description>original-user-description</description>
</assembly>
'@
    $mergeManifest = Join-Path $directory "extra.manifest"
    Write-FixtureFile $mergeManifest @'
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application><supportedOS Id="{11111111-1111-1111-1111-111111111111}" /></application>
  </compatibility>
</assembly>
'@
    $assets = Join-Path $directory "obj\project.assets.json"
    # NuGet library identifiers are package/version data, not filesystem paths.
    Write-FixtureFile $assets @'
{
  "version": 3,
  "libraries": {
    "Fixture.TransitiveGenerator/1.0.0": { "type": "package" }
  }
}
'@
    $project = Join-Path $directory "w.proj"
    $projectXml = @'
<Project>
  <PropertyGroup>
    <Platform>x64</Platform>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <IntermediateOutputPath>obj\</IntermediateOutputPath>
    <ProjectAssetsFile>$(MSBuildProjectDirectory)\obj\project.assets.json</ProjectAssetsFile>
    <ApplicationManifest>$(MSBuildProjectDirectory)\user.manifest</ApplicationManifest>
    <ManifestTool>@MANIFEST_TOOL@</ManifestTool>
    <IncludeExtraComponent Condition="'$(IncludeExtraComponent)' == ''">false</IncludeExtraComponent>
  </PropertyGroup>
  <ItemGroup>
    <WindowsAppSdkComponentPackages Include="$(MSBuildProjectDirectory)\a\" />
    <WindowsAppSdkComponentPackages Include="$(MSBuildProjectDirectory)\b\" Condition="'$(IncludeExtraComponent)' == 'true'" />
    <MicrosoftWindowsAppSDKFilesExcluded Include="$(MSBuildProjectDirectory)\a\runtimes-framework\win-x64\native\Excluded.dll" />
    <None Include="$(MSBuildProjectDirectory)\existing.txt" Link="keep\existing.txt" CopyToOutputDirectory="Never" />
    <MtMergeInputs Include="$(MSBuildProjectDirectory)\extra.manifest" />
  </ItemGroup>
  <Target Name="ResolveReferences" />
  <Import Project="i.targets" />
  <Import Project="u.targets" />
  <Import Project="o.targets" />
  <PropertyGroup>
    <MSBuildAllProjects>$(MSBuildProjectFullPath);$(MSBuildProjectDirectory)\i.targets;$(MSBuildProjectDirectory)\u.targets;$(MSBuildProjectDirectory)\o.targets</MSBuildAllProjects>
  </PropertyGroup>
  <Target Name="CapturePayload" DependsOnTargets="AddMicrosoftWindowsAppSDKPayloadFilesFromComponents">
    <WriteLinesToFile File="$(MSBuildProjectDirectory)\payload.txt"
      Lines="@(None->'None|%(Identity)|%(RecursiveDir)|%(Link)|%(CopyToOutputDirectory)');@(ReferenceCopyLocalPaths->'ReferenceCopyLocalPaths|%(Identity)|%(RecursiveDir)|%(Link)|%(CopyToOutputDirectory)')"
      Overwrite="true" />
  </Target>
</Project>
'@
    Write-FixtureFile $project ($projectXml.Replace("@MANIFEST_TOOL@", (ConvertTo-XmlText $script:ManifestTool)))
    $dlls = [Collections.Generic.List[string]]::new()
    foreach ($relative in @("Fixture.Activation.dll", "Fixture.Proxy.dll", "plain\Helper.dll", "Excluded.dll")) {
        $dlls.Add((Join-Path $native $relative))
    }
    $fixture = [pscustomobject]@{
        Directory = $directory; Project = $project; Native = $native; Component = $component
        Fragment = $fragment; ExtraFragment = $extraFragment; ExtraDll = $extraDll; Dlls = $dlls
        ClassName = "Fixture.Alpha"; ExtraComponent = $false; Redirect = $false
        UserManifest = $userManifest; Description = "original-user-description"
        MergeManifest = $mergeManifest; SupportedOS = "{11111111-1111-1111-1111-111111111111}"
        ProjectAssetsFile = $assets
        Upstream = $upstream; Override = $override; Import = $import
        AppManifest = Join-Path $directory "obj\Manifests\app.manifest"
        SDKManifest = Join-Path $directory "obj\Manifests\WindowsAppSDK.manifest"
    }
    foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
        [IO.File]::SetLastWriteTimeUtc($file.FullName, $script:OldInputTime)
    }
    return $fixture
}

function Get-RegistrationRecord {
    param([string]$Name, [bool]$Redirect, [string]$Children = "")
    $attributes = @("name=$Name")
    if ($Redirect) { $attributes += "loadFrom=%MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY%$Name" }
    [Array]::Sort($attributes, [StringComparer]::Ordinal)
    return "urn:schemas-microsoft-com:asm.v3|file|$($attributes -join ';')|$Children"
}

function Assert-WinUIRegistrations {
    param([object]$Fixture)
    $expected = [Collections.Generic.List[string]]::new()
    $expected.Add((Get-RegistrationRecord "Fixture.Activation.dll" $Fixture.Redirect `
        "urn:schemas-microsoft-com:winrt.v1|activatableClass|name=$($Fixture.ClassName);threadingModel=both"))
    $expected.Add((Get-RegistrationRecord "Fixture.Proxy.dll" $Fixture.Redirect (
        "urn:schemas-microsoft-com:asm.v3|comClass|clsid={11111111-2222-3333-4444-555555555555}," +
        "urn:schemas-microsoft-com:asm.v3|comInterfaceProxyStub|iid={66666666-7777-8888-9999-000000000000};name=Fixture.IProxy")))
    if ($Fixture.ExtraComponent) {
        $expected.Add((Get-RegistrationRecord "Fixture.Beta.dll" $Fixture.Redirect `
            "urn:schemas-microsoft-com:winrt.v1|activatableClass|name=Fixture.Beta;threadingModel=both"))
    }
    if ($Fixture.Redirect) {
        # Preserve the pinned upstream generator's FrameworkDlls identities, including
        # unregistered DLLs. These are deliberately independent of the copied-item exclusions.
        foreach ($dll in $Fixture.Dlls) { $expected.Add((Get-RegistrationRecord $dll $true)) }
        if ($Fixture.ExtraComponent) { $expected.Add((Get-RegistrationRecord $Fixture.ExtraDll $true)) }
    }
    $expectedArray = $expected.ToArray()
    [Array]::Sort($expectedArray, [StringComparer]::Ordinal)
    foreach ($path in @($Fixture.SDKManifest, $Fixture.AppManifest)) {
        Assert-TestCondition ([IO.File]::Exists($path)) "The real generator and mt.exe must produce '$path'."
        $document = [xml][IO.File]::ReadAllText($path)
        $actual = [Collections.Generic.List[string]]::new()
        foreach ($node in $document.SelectNodes("/*[local-name()='assembly']/*[local-name()='file']")) {
            $attributes = [string[]]@($node.Attributes | Where-Object NamespaceURI -ne "http://www.w3.org/2000/xmlns/" |
                ForEach-Object { "$($_.LocalName)=$($_.Value)" })
            [Array]::Sort($attributes, [StringComparer]::Ordinal)
            $children = @(foreach ($child in $node.ChildNodes | Where-Object NodeType -eq "Element") {
                $childAttributes = [string[]]@($child.Attributes | Where-Object NamespaceURI -ne "http://www.w3.org/2000/xmlns/" |
                    ForEach-Object { "$($_.LocalName)=$($_.Value)" })
                [Array]::Sort($childAttributes, [StringComparer]::Ordinal)
                "$($child.NamespaceURI)|$($child.LocalName)|$($childAttributes -join ';')"
            })
            $actual.Add("$($node.NamespaceURI)|$($node.LocalName)|$($attributes -join ';')|$($children -join ',')")
        }
        $actualArray = $actual.ToArray()
        [Array]::Sort($actualArray, [StringComparer]::Ordinal)
        Assert-Equal ($expectedArray -join "`n") ($actualArray -join "`n") "Exact DLL, WinRT activation, COM registration, namespace, and redirect associations must agree in '$path'."
    }
    $merged = [xml][IO.File]::ReadAllText($Fixture.AppManifest)
    $identity = $merged.SelectSingleNode("/*[local-name()='assembly']/*[local-name()='assemblyIdentity']")
    Assert-Equal "Fixture.Application|1.2.3.4|amd64" "$($identity.GetAttribute('name'))|$($identity.GetAttribute('version'))|$($identity.GetAttribute('processorArchitecture'))" "mt.exe must preserve the user manifest identity."
    Assert-Equal $Fixture.Description ($merged.SelectSingleNode("/*[local-name()='assembly']/*[local-name()='description']").InnerText) "mt.exe must merge the current user manifest."
    $supported = @($merged.SelectNodes("//*[local-name()='supportedOS']") | ForEach-Object { $_.GetAttribute("Id") })
    Assert-Equal $Fixture.SupportedOS ($supported -join ",") "Preexisting MtMergeInputs must survive the override."
}

function Set-WinUICheckpoint {
    param([object]$Fixture)
    # Give all owned inputs/cache an old timestamp and both outputs a timestamp
    # newer than the read-only mt.exe. A changed membership cache will be newer;
    # an older-added input will not. No sleeps, clock-resolution races, or cache edits.
    $stamp = [IO.File]::GetLastWriteTimeUtc($script:ManifestTool).AddDays(1)
    Assert-TestCondition ($stamp -lt [DateTime]::UtcNow) "The pinned manifest tool must predate the controlled output checkpoint."
    foreach ($file in Get-ChildItem -LiteralPath $Fixture.Directory -Recurse -File) {
        [IO.File]::SetLastWriteTimeUtc($file.FullName, $script:OldInputTime)
    }
    foreach ($path in @($Fixture.AppManifest, $Fixture.SDKManifest)) {
        Assert-TestCondition ([IO.File]::Exists($path)) "A checkpoint requires generated output '$path'."
        [IO.File]::SetLastWriteTimeUtc($path, $stamp)
    }
    return $stamp.Ticks
}

function Assert-WinUIRegenerated {
    param([object]$Fixture, [long]$Checkpoint)
    Assert-WinUIRegistrations $Fixture
    foreach ($path in @($Fixture.AppManifest, $Fixture.SDKManifest)) {
        Assert-TestCondition ([IO.File]::GetLastWriteTimeUtc($path).Ticks -gt $Checkpoint) "Changed inputs must regenerate and advance '$path' beyond checkpoint $Checkpoint; actual $([IO.File]::GetLastWriteTimeUtc($path).Ticks)."
    }
}

function Get-ManifestState {
    param([object]$Fixture)
    return @(foreach ($path in @($Fixture.AppManifest, $Fixture.SDKManifest)) {
        "$path|$([IO.File]::GetLastWriteTimeUtc($path).Ticks)|$((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)"
    }) -join "`n"
}

function Assert-WinUIPayload {
    param([object]$Fixture, [string]$MetadataName)
    $expected = [Collections.Generic.List[string]]::new()
    $existing = Join-Path $Fixture.Directory "existing.txt"
    $expected.Add("None|$existing||keep\existing.txt|Never")
    foreach ($relative in @("Fixture.Activation.dll", "Fixture.Proxy.dll", "plain\Helper.dll",
        "workloads.fixture.json", "restartAgent.exe", "map.html", "en-US\Strings.mui",
        "Assets\icon.png", "schema\Native.winmd", "views\Page.xaml", "views\Page.xbf", "Assets\resources.pri")) {
        $identity = Join-Path $Fixture.Native $relative
        $recursive = if ($relative.Contains("\")) { $relative.Substring(0, $relative.LastIndexOf("\") + 1) } else { "" }
        $itemType = if ($relative.EndsWith(".pri")) { "ReferenceCopyLocalPaths" } else { "None" }
        $expected.Add("$itemType|$identity|$recursive|$relative|PreserveNewest")
    }
    foreach ($relative in @("metadata\Root.winmd", $MetadataName)) {
        $identity = Join-Path $Fixture.Component $relative
        $link = [IO.Path]::GetFileName($relative)
        $expected.Add("None|$identity||$link|PreserveNewest")
    }
    $actual = [string[]][IO.File]::ReadAllLines((Join-Path $Fixture.Directory "payload.txt"))
    $expectedArray = $expected.ToArray()
    [Array]::Sort($expectedArray, [StringComparer]::Ordinal)
    [Array]::Sort($actual, [StringComparer]::Ordinal)
    Assert-Equal ($expectedArray -join "`n") ($actual -join "`n") "Payload identities, exclusions, RecursiveDir, Link, copy policy, PRI routing, and existing items must match upstream semantics exactly."
}

$cases = [ordered]@{
    CoverageSettingsPreserveExactBytesAndTimestamp = {
        $fixture = New-CoverageFixture
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "initial"
        Assert-Coverage $fixture
        & (Join-Path $script:RepositoryRoot "eng\validate-code-coverage-settings.ps1") `
            -SettingsFiles @($fixture.Output) -RepositoryRoot $fixture.Root `
            -ProjectFile $fixture.Project -NuGetPackageRoot $fixture.Packages
        $bytes = [IO.File]::ReadAllBytes($fixture.Output)
        # A fixed old timestamp detects any rewrite without sleeping or measuring elapsed time.
        $stamp = [DateTime]::new(2001, 2, 3, 4, 5, 6, [DateTimeKind]::Utc)
        [IO.File]::SetLastWriteTimeUtc($fixture.Output, $stamp)
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "unchanged"
        Assert-BytesEqual $bytes ([IO.File]::ReadAllBytes($fixture.Output)) "An unchanged coverage build must preserve exact bytes."
        Assert-Equal $stamp.Ticks ([IO.File]::GetLastWriteTimeUtc($fixture.Output).Ticks) "An unchanged coverage build must not rewrite the file."
        $corrupted = [byte[]]$bytes.Clone()
        $corrupted[3] = [byte][char]"!"
        [IO.File]::WriteAllBytes($fixture.Output, $corrupted)
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "same-length-corrupted-output"
        Assert-Coverage $fixture
    }
    CoverageSettingsRefreshInputsAndRecoverMissingOutput = {
        $fixture = New-CoverageFixture
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "initial"
        Assert-Coverage $fixture
        $initialLength = ([IO.FileInfo]$fixture.Output).Length
        $fixture.Module = "Bravo.Core"
        $properties = @("FixtureModule=$($fixture.Module)")
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "same-length-module-change" $properties
        Assert-Coverage $fixture
        Assert-Equal $initialLength ([IO.FileInfo]$fixture.Output).Length "The module-change witness must preserve length, not just exercise a size change."
        $fixture.Linked = $true
        $properties += "CodeCoverageIncludeSourceLinkedProductionCode=true"
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "opt-in" $properties
        Assert-Coverage $fixture
        $fixture.Packages = ($fixture.Root.Replace("\", "/") + "/new packages+[v2]&cache/")
        $fixture.Artifacts = ($fixture.Root.Replace("\", "/") + "/new artifacts+[v2]&cache/")
        $properties += @("NuGetPackageRoot=$($fixture.Packages)", "ArtifactsDir=$($fixture.Artifacts)")
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "portable-path-inputs" $properties
        Assert-Coverage $fixture
        $settings = [xml][IO.File]::ReadAllText($fixture.Output)
        $patterns = @($settings.SelectNodes("/Configuration/CodeCoverage/Sources/Include/Source") | ForEach-Object InnerText)
        foreach ($path in @("/_/src/Linked.cs", "\_\src\Linked.cs")) {
            Assert-TestCondition ([regex]::IsMatch($path, $patterns[1])) "Mapped production sources must accept portable slashes: '$path'."
        }
        Assert-TestCondition (-not [regex]::IsMatch("/_/test/Linked.cs", $patterns[1])) "Mapped tests must not enter production coverage."
        [IO.File]::Delete($fixture.Output)
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "missing-output" $properties
        Assert-Coverage $fixture
        $fixture.Linked = $false
        $properties[1] = "CodeCoverageIncludeSourceLinkedProductionCode=false"
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings" "opt-out" $properties
        Assert-Coverage $fixture
    }
    CoverageWriterPreservesValueMetadataAndProjectRelativePath = {
        $fixture = New-CoverageFixture
        $null = Invoke-FixtureBuild $fixture.Project "_GenerateCodeCoverageSettings;WriteMetadataProbe" "relative-and-metadata" -MultiThreaded
        Assert-Coverage $fixture
        $expected = Get-BomBytes (('<probe>', '  <path>^[\\/]root[a-z]+\.(dll|exe)$ /_/src/ &amp; tail</path>', '</probe>' -join [Environment]::NewLine) + [Environment]::NewLine)
        Assert-BytesEqual $expected ([IO.File]::ReadAllBytes((Join-Path $fixture.Directory "probe.config"))) "The writer must serialize Value metadata, not path-normalized item identities."
        Assert-TestCondition (-not [IO.File]::Exists((Join-Path $script:WorkingDirectory "obj\coverage.config"))) "Coverage must never be routed to process CWD."
    }
    CoverageParallelProjectsStayInProcess = {
        Invoke-ParallelCoverageCase
    }
    CoverageParallelProjectsStayInProcessStrict = {
        Invoke-ParallelCoverageCase -StrictTaskHost
    }
    WinUIPayloadItemsPreserveIdentityAndMetadata = {
        $fixture = New-WinUIFixture
        $null = Invoke-FixtureBuild $fixture.Project "CapturePayload" "default-payload"
        Assert-WinUIPayload $fixture "metadata\10.0.18362.0\Current.winmd"
        $null = Invoke-FixtureBuild $fixture.Project "CapturePayload" "older-platform-payload" @("TargetPlatformVersion=10.0.17763.0")
        Assert-WinUIPayload $fixture "metadata\10.0.17763.0\Older.winmd"
    }
    WinUIManifestsPreserveRegistrationsAndTimestamp = {
        $fixture = New-WinUIFixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "initial"
        Assert-WinUIRegistrations $fixture
        $null = Set-WinUICheckpoint $fixture
        $state = Get-ManifestState $fixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "unchanged"
        Assert-WinUIRegistrations $fixture
        Assert-Equal $state (Get-ManifestState $fixture) "Unchanged WinUI builds must preserve BOTH generated manifest timestamps and bytes."
    }
    WinUIFragmentAndComponentMembershipRefreshRegistrations = {
        $fixture = New-WinUIFixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "initial"
        Assert-WinUIRegistrations $fixture
        $stamp = Set-WinUICheckpoint $fixture
        $fixture.ClassName = "Fixture.Alpha.Edited"
        Write-ComponentFragment $fixture.Fragment $fixture.ClassName -Proxy
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "fragment-edit"
        Assert-WinUIRegenerated $fixture $stamp
        $stamp = Set-WinUICheckpoint $fixture
        $fixture.ExtraComponent = $true
        Assert-TestCondition ([IO.File]::GetLastWriteTimeUtc($fixture.ExtraFragment).Ticks -lt $stamp) "The newly included component fragment must be older than the outputs."
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "older-component-added" @("IncludeExtraComponent=true")
        Assert-WinUIRegenerated $fixture $stamp
        $stamp = Set-WinUICheckpoint $fixture
        $fixture.ExtraComponent = $false
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "component-removed"
        Assert-WinUIRegenerated $fixture $stamp
    }
    WinUISingleFileAndDllMembershipRefreshRegistrations = {
        $fixture = New-WinUIFixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "initial"
        Assert-WinUIRegistrations $fixture
        $stamp = Set-WinUICheckpoint $fixture
        $fixture.Redirect = $true
        $properties = @("PublishSingleFile=true")
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "single-file-on" $properties
        Assert-WinUIRegenerated $fixture $stamp
        $stamp = Set-WinUICheckpoint $fixture
        $added = Join-Path $fixture.Native "Older.Added.dll"
        Write-FixtureFile $added "Inert older-added DLL"
        [IO.File]::SetLastWriteTimeUtc($added, $script:OldInputTime)
        $fixture.Dlls.Add($added)
        Assert-TestCondition ([IO.File]::GetLastWriteTimeUtc($added).Ticks -lt $stamp) "The newly added DLL must be older than the outputs."
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "older-dll-added" $properties
        Assert-WinUIRegenerated $fixture $stamp
        $stamp = Set-WinUICheckpoint $fixture
        [IO.File]::Delete($added)
        $null = $fixture.Dlls.Remove($added)
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "dll-removed" $properties
        Assert-WinUIRegenerated $fixture $stamp
        $stamp = Set-WinUICheckpoint $fixture
        $fixture.Redirect = $false
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "single-file-off"
        Assert-WinUIRegenerated $fixture $stamp
    }
    WinUIMissingManifestsAndUserManifestChangesRecover = {
        $fixture = New-WinUIFixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "initial"
        Assert-WinUIRegistrations $fixture
        foreach ($path in @($fixture.SDKManifest, $fixture.AppManifest)) {
            $stamp = Set-WinUICheckpoint $fixture
            [IO.File]::Delete($path)
            $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "missing-manifest"
            Assert-WinUIRegenerated $fixture $stamp
        }
        $stamp = Set-WinUICheckpoint $fixture
        $fixture.Description = "changed-user-description"
        $text = [IO.File]::ReadAllText($fixture.UserManifest).Replace("original-user-description", $fixture.Description)
        Write-FixtureFile $fixture.UserManifest $text
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "user-manifest-edit"
        Assert-WinUIRegenerated $fixture $stamp
    }
    WinUIImportedInputsInvalidateRegistration = {
        $fixture = New-WinUIFixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "initial"
        Assert-WinUIRegistrations $fixture
        foreach ($path in @($fixture.Upstream, $fixture.Override, $fixture.Import, $fixture.Project)) {
            $stamp = Set-WinUICheckpoint $fixture
            [IO.File]::SetLastWriteTimeUtc($path, [DateTime]::UtcNow)
            $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "import-timestamp-change"
            Assert-WinUIRegenerated $fixture $stamp
        }
        $stamp = Set-WinUICheckpoint $fixture
        $assets = [IO.File]::ReadAllText($fixture.ProjectAssetsFile)
        $updatedAssets = $assets.Replace("Fixture.TransitiveGenerator/1.0.0", "Fixture.TransitiveGenerator/2.0.0")
        Assert-TestCondition ($assets -cne $updatedAssets) "The assets transition must change the transitive generator package version."
        # No import/project/member path changes: MSBuildAllProjects and the membership
        # state stay identical. Only ProjectAssetsFile contents and timestamp change.
        Write-FixtureFile $fixture.ProjectAssetsFile $updatedAssets
        Assert-TestCondition ([IO.File]::GetLastWriteTimeUtc($fixture.ProjectAssetsFile).Ticks -gt $stamp) "The changed assets file must be newer than the outputs."
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "assets-dependency-graph-change"
        Assert-WinUIRegenerated $fixture $stamp
        $state = Get-ManifestState $fixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "unchanged-after-assets-change"
        Assert-Equal $state (Get-ManifestState $fixture) "A consumed dependency-graph change must not cause perpetual regeneration."
        $stamp = Set-WinUICheckpoint $fixture
        $fixture.SupportedOS = "{22222222-2222-2222-2222-222222222222}"
        $text = [IO.File]::ReadAllText($fixture.MergeManifest).Replace("{11111111-1111-1111-1111-111111111111}", $fixture.SupportedOS)
        Write-FixtureFile $fixture.MergeManifest $text
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "extra-merge-input-edit"
        Assert-WinUIRegenerated $fixture $stamp
        $state = Get-ManifestState $fixture
        $null = Invoke-FixtureBuild $fixture.Project "CreateWinRTRegistration" "unchanged-after-invalidation"
        Assert-Equal $state (Get-ManifestState $fixture) "A consumed changed input must not cause perpetual regeneration."
    }
}

function Invoke-ParallelCoverageCase {
    param([switch]$StrictTaskHost)
    $fixtures = @(foreach ($index in 1..4) {
        New-CoverageFixture "Parallel$index" "Distinct$index.Core" ($index % 2 -eq 0)
    })
    $projects = ($fixtures | ForEach-Object { ConvertTo-XmlText $_.Project }) -join ";"
    $traversal = Join-Path $script:CaseDirectory "parallel.proj"
    Write-FixtureFile $traversal @"
<Project>
  <ItemGroup><CoverageProject Include="$projects" /></ItemGroup>
  <Target Name="RunCoverage">
    <MSBuild Projects="@(CoverageProject)" Targets="_GenerateCodeCoverageSettings" BuildInParallel="true" />
  </Target>
</Project>
"@
    $log = Invoke-FixtureBuild $traversal "RunCoverage" "parallel" -MultiThreaded -StrictTaskHost:$StrictTaskHost
    foreach ($fixture in $fixtures) { Assert-Coverage $fixture }
    Assert-TestCondition (-not [IO.File]::Exists((Join-Path $script:WorkingDirectory "obj\coverage.config"))) "Parallel coverage must not share a CWD-relative output."
    Assert-TestCondition (-not [regex]::IsMatch($log, '(?im)Using\s+"WriteCodeCoverageSettings".*task host|Task\s+"WriteCodeCoverageSettings".*(out.of.process|task host)')) "Coverage must not execute in an external task host."
}

if (-not $ArtifactsDirectory) { throw "Supply an owned scratch directory with -ArtifactsDirectory." }
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$scratchDirectory = [IO.Path]::GetFullPath($ArtifactsDirectory)
if ($scratchDirectory -ieq $RepositoryRoot -or $scratchDirectory.StartsWith($RepositoryRoot.TrimEnd("\", "/") + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Fixtures must be outside the repository: '$scratchDirectory'."
}
if (-not $CoverageTargetsPath) { $CoverageTargetsPath = Join-Path $RepositoryRoot "test\Directory.Build.targets" }
if (-not $WinUITargetsPath) { $WinUITargetsPath = Join-Path $RepositoryRoot "eng\WinUI.Incremental.targets" }
if (-not $PackageRoot) { $PackageRoot = Join-Path $HOME ".nuget\packages" }
if (-not $ManifestTool) {
    $ManifestTool = Join-Path $PackageRoot "microsoft.windows.sdk.buildtools\10.0.26100.4654\bin\10.0.26100.0\x64\mt.exe"
}
$upstreamTargets = Join-Path $PackageRoot "microsoft.windowsappsdk.base\1.8.250831001\buildTransitive\Microsoft.WindowsAppSDK.SelfContained.targets"
$runRoot = Join-Path $scratchDirectory ("r-" + [guid]::NewGuid().ToString("N"))
[IO.Directory]::CreateDirectory((Join-Path $runRoot "inputs")) | Out-Null
$script:RepositoryRoot = $RepositoryRoot
$script:ScratchDirectory = $scratchDirectory
$script:OldInputTime = [DateTime]::new(2001, 2, 3, 4, 5, 6, [DateTimeKind]::Utc)
$script:Invocations = [Collections.Generic.List[object]]::new()
$script:CoverageSnapshot = Join-Path $runRoot "inputs\coverage.targets"
$script:WinUISnapshot = Join-Path $runRoot "inputs\WinUI.Incremental.targets"
$script:UpstreamSnapshot = Join-Path $runRoot "inputs\Microsoft.WindowsAppSDK.SelfContained.targets"
$script:ManifestTool = [IO.Path]::GetFullPath($ManifestTool)
$selected = @($cases.Keys | Where-Object {
    $name = $_
    @($TestName | Where-Object { $name -like $_ }).Count -gt 0
})
if ($selected.Count -eq 0) { throw "No regression cases match: $($TestName -join ', ')." }
$sources = @{}
$sourceCopies = @{}
if (@($selected | Where-Object { $_ -like "Coverage*" }).Count -gt 0) {
    $sourceCopies[[IO.Path]::GetFullPath($CoverageTargetsPath)] = $script:CoverageSnapshot
}
if (@($selected | Where-Object { $_ -like "WinUI*" }).Count -gt 0) {
    $sourceCopies[[IO.Path]::GetFullPath($WinUITargetsPath)] = $script:WinUISnapshot
    $sourceCopies[[IO.Path]::GetFullPath($upstreamTargets)] = $script:UpstreamSnapshot
    if (-not [IO.File]::Exists($script:ManifestTool)) { throw "The pinned real manifest tool is missing: '$script:ManifestTool'." }
    $sources[$script:ManifestTool] = (Get-FileHash -LiteralPath $script:ManifestTool -Algorithm SHA256).Hash
}
foreach ($source in $sourceCopies.Keys) {
    $sources[$source] = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    [IO.File]::Copy($source, $sourceCopies[$source])
}
$sdk = (Get-Content -LiteralPath (Join-Path $RepositoryRoot "global.json") -Raw | ConvertFrom-Json).sdk.version
$script:SDKVersion = $sdk
Write-FixtureFile (Join-Path $runRoot "global.json") (ConvertTo-Json -InputObject @{
    sdk = @{ version = $sdk; rollForward = "disable"; allowPrerelease = $true }
})
$outcomes = [Collections.Generic.List[object]]::new()
$parentCwd = [Environment]::CurrentDirectory
$parentEnvironment = [Environment]::GetEnvironmentVariables()
Write-Output "Build incrementality evidence: $runRoot"
foreach ($name in $selected) {
    $script:CaseName = $name
    # Keep native mt.exe and CreateProcess paths below MAX_PATH even under a long session root.
    # Case identities live in results.json, independently of these per-test GUID output roots.
    $script:CaseDirectory = Join-Path $runRoot ([guid]::NewGuid().ToString("N"))
    $script:WorkingDirectory = Join-Path $script:CaseDirectory "cwd"
    [IO.Directory]::CreateDirectory($script:WorkingDirectory) | Out-Null
    $outcome = "Passed"
    $failure = $null
    try {
        & $cases[$name]
        Write-Output "PASS $name"
    } catch {
        $outcome = "Failed"
        $failure = $_.Exception.Message
        Write-Output "FAIL ${name}: $failure"
    }
    $outcomes.Add([pscustomobject]@{ Name = $name; Outcome = $outcome; Failure = $failure; Directory = $script:CaseDirectory })
}
$preserved = $true
foreach ($source in $sources.Keys) {
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -cne $sources[$source]) {
        $preserved = $false
        Write-Output "FAIL SourceChangedDuringRun: $source"
    }
    $environmentPreserved = $parentCwd -ceq [Environment]::CurrentDirectory
    $currentEnvironment = [Environment]::GetEnvironmentVariables()
    if ($parentEnvironment.Count -ne $currentEnvironment.Count) { $environmentPreserved = $false }
    foreach ($key in $parentEnvironment.Keys) {
        if ($parentEnvironment[$key] -cne $currentEnvironment[$key]) { $environmentPreserved = $false }
    }
    if (-not $environmentPreserved) { Write-Output "FAIL ParentEnvironmentChanged: child-only environment/CWD isolation was violated." }
}
$failed = @($outcomes | Where-Object Outcome -eq "Failed").Count
$resultPath = Join-Path $runRoot "results.json"
Write-FixtureFile $resultPath (ConvertTo-Json -Depth 12 -InputObject ([ordered]@{
    SDK = $sdk; RepositoryRoot = $RepositoryRoot; Selected = $selected
    HarnessSHA256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    Executed = $outcomes.Count; Passed = $outcomes.Count - $failed; Failed = $failed; Skipped = 0
    SourceHashes = $sources; SourceHashesPreserved = $preserved
    SourceSnapshots = $sourceCopies; ParentEnvironmentPreserved = $environmentPreserved
    Cases = @($outcomes); Invocations = @($script:Invocations)
}))
$parsed = [IO.File]::ReadAllText($resultPath) | ConvertFrom-Json
Assert-TestCondition ($parsed.Executed -eq $selected.Count -and $parsed.Cases.Count -eq $selected.Count) "The result artifact must account for every selected test."
Write-Output "Build incrementality tests: $($parsed.Passed) passed; $($parsed.Failed) failed; $($parsed.Executed) executed; 0 skipped."
Write-Output "Results: $resultPath"
if ($failed -gt 0 -or -not $preserved -or -not $environmentPreserved) { exit 1 }
exit 0
