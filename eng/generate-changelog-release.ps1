<#
.SYNOPSIS
    Create release notes link and changelog entries for MSTest and Testing Platform.

.EXAMPLE
    Assuming you are on branch rel/3.4 and you want to create the release notes, run
    .\write-release-notes -MSTestVersion 3.4.0 -PlatformVersion 1.2.0
    it will create release notes between last commit of current branch and last release
#>

[CmdletBinding()]
param
(
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^\d+\.\d+\.\d+(-preview-\d{8}-\d{2})?$")][string] $MSTestVersion,
    [Parameter(Mandatory=$true)]
    [ValidatePattern("^\d+\.\d+\.\d+(-preview-\d{8}-\d{2})?$")][string] $PlatformVersion
)

$Path = "."
$repoUrl = $(if ((git -C $Path remote -v) -match "upstream") {
        git -C $Path remote get-url --push upstream
    }
    else {
        git -C $Path remote get-url --push origin
    }) -replace "\.git$"

# list all tags on this branch ordered by creator date to get the latest, stable or pre-release tag.
# For stable release we choose only tags without any dash, for pre-release we choose all tags.
$tags = git -C $Path tag -l --sort=refname | Where-Object { $_ -match "v\d+\.\d+\.\d+.*" -and (-not $Stable -or $_ -notlike '*-*') }

if ([string]::IsNullOrWhiteSpace($MSTestVersion)) {
    # normally we show changes between the latest two tags
    $start, $end = $tags | Select-Object -Last 2
    Write-Host "$start -- $end"
    $tag = $end
}
else {
    # in CI we don't have the tag yet, so we show changes between the most recent tag, and this commit
    # we figure out the tag from the package version that is set by vsts-prebuild
    $start = $tags | Select-Object -Last 1
    $end = git -C $Path rev-parse HEAD
    $tag = "v$MSTestVersion"
}

# # override the tags to use if you need
# $start = "v16.8.0-preview-20200812-03"
# $end = $tag = "v16.8.0-preview-20200921-01"

Write-Host "Generating release notes for $start..$end$(if ($HasPackageVersion) { " (expected tag: $tag)" })"

$sourceBranch = $branch = git -C $Path rev-parse --abbrev-ref HEAD
if ($sourceBranch -eq "HEAD") {
    # when CI checks out just the single commit, https://docs.microsoft.com/azure/devops/pipelines/build/variables?view=azure-devops&tabs=yaml
    $sourceBranch = $env:BUILD_SOURCEBRANCH -replace "^refs/heads/"
}

if ([string]::IsNullOrWhiteSpace($branch)) {
    throw "Branch is null or empty!"
}

if ([string]::IsNullOrWhiteSpace($sourceBranch)) {
    throw "SourceBranch is null or empty!"
}

Write-Host "Branch is $branch"
Write-Host "SourceBranch is $sourceBranch"

$discard = @(
    "^Update dependencies from https:\/\/",
    "^\[.+\] Update dependencies from",
    "^LEGO: Pull request from lego",
    "^Localized file check-in by OneLocBuild Task:",
    "^Juno: check in to lego"
) -join "|"

$prUrl = "$repoUrl/pull/"
# $tagVersionNumber = $tag -replace '^v'
# using .. because I want to know the changes that are on this branch, but don't care about the changes that I don't have https://stackoverflow.com/a/24186641/3065397
$log = (git -C $Path log "$start..$end" --pretty="format:%s by @%ae" --first-parent)
$issues = $log | ForEach-Object {
    if ($_ -notmatch $discard) {
        if ($_ -match '^(?<message>.+)\s\(#(?<pr>\d+)\) by @(?<author>.+)?$') {
            $message = "* $($matches.message)"

            if ($matches.author) {
                $message += " by @$($matches.author)"
            }

            if ($matches.pr) {
                $pr = $matches.pr
                $message += " in [#$pr]($prUrl$pr)"
            }

            if ($_ -like 'fix *') {
                [pscustomobject]@{ category = "fix"; text = $message }
            } else {
                [pscustomobject]@{ category = "add"; text = $message }
            }
        }
        else {
            [pscustomobject]@{ category = "add"; text = "* $_" }
        }
    }
} | Group-Object -Property category -AsHashTable
$date = Get-Date -Format "yyyy-MM-dd"
$output = @"
-------------------------------
MSTest Version: $MSTestVersion
-------------------------------

See the release notes [here](https://github.com/microsoft/testfx/blob/main/docs/Changelog.md#$MSTestVersion).

-------------------------------

## <a name="$MSTestVersion" />[$MSTestVersion] - $date

See full log [here]($repoUrl/compare/$start...$tag)

### Added

$($issues.add.text -join "`n")

### Fixed

$($issues.fix.text -join "`n")

### Artifacts

* MSTest: [$MSTestVersion](https://www.nuget.org/packages/MSTest/$MSTestVersion)
* MSTest.TestFramework: [$MSTestVersion](https://www.nuget.org/packages/MSTest.TestFramework/$MSTestVersion)
* MSTest.TestAdapter: [$MSTestVersion](https://www.nuget.org/packages/MSTest.TestAdapter/$MSTestVersion)
* MSTest.Analyzers: [$MSTestVersion](https://www.nuget.org/packages/MSTest.Analyzers/$MSTestVersion)
* MSTest.Sdk: [$MSTestVersion](https://www.nuget.org/packages/MSTest.Sdk/$MSTestVersion)
* Microsoft.Testing.Extensions.CrashDump: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/$PlatformVersion)
* Microsoft.Testing.Extensions.HangDump: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/$PlatformVersion)
* Microsoft.Testing.Extensions.HotReload: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/$PlatformVersion)
* Microsoft.Testing.Extensions.Retry: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/$PlatformVersion)
* Microsoft.Testing.Extensions.TrxReport: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/$PlatformVersion)

-------------------------------
Testing Platform Version: $PlatformVersion
-------------------------------

See the release notes [here](https://github.com/microsoft/testfx/blob/main/docs/Changelog-TestingPlatform.md#$PlatformVersion).

-------------------------------

## <a name="$PlatformVersion" />[$PlatformVersion] - $date

See full log [here]($repoUrl/compare/$start...$tag)

### Added

$($issues.add.text -join "`n")

### Fixed

$($issues.fix.text -join "`n")

### Artifacts

* Microsoft.Testing.Extensions.CrashDump: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.CrashDump/$PlatformVersion)
* Microsoft.Testing.Extensions.HangDump: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HangDump/$PlatformVersion)
* Microsoft.Testing.Extensions.HotReload: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.HotReload/$PlatformVersion)
* Microsoft.Testing.Extensions.Retry: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Retry/$PlatformVersion)
* Microsoft.Testing.Extensions.Telemetry: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.Telemetry/$PlatformVersion)
* Microsoft.Testing.Extensions.TrxReport: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/$PlatformVersion)
* Microsoft.Testing.Extensions.TrxReport.Abstractions: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport.Abstractions/$PlatformVersion)
* Microsoft.Testing.Extensions.VSTestBridge: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Extensions.VSTestBridge/$PlatformVersion)
* Microsoft.Testing.Platform: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Platform/$PlatformVersion)
* Microsoft.Testing.Platform.MSBuild: [$PlatformVersion](https://www.nuget.org/packages/Microsoft.Testing.Platform.MSBuild/$PlatformVersion)

"@

$output
$output | clip
