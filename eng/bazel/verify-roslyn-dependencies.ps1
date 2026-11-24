# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

#requires -Version 7.2

[CmdletBinding()]
param([switch] $SelfTest)

$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

function Get-RoslynPins([string] $Content, [bool] $LockFile) {
    $pins = @{}
    $group = 'main'
    foreach ($line in $Content -split '\r?\n') {
        if ($line -match '^group ([^ ]+)\s*$') {
            $group = $Matches[1].ToLowerInvariant()
        }
        elseif (($LockFile -and $line -match '^    (Microsoft\.CodeAnalysis(?:\.[^ ]+)?) \((\d+(?:\.\d+){1,3})\)') -or
            (-not $LockFile -and $line -match '^\s*nuget (Microsoft\.CodeAnalysis(?:\.[^ ]+)?) == (\d+(?:\.\d+){1,3})\s*$')) {
            $key = "$group/$($Matches[1])"
            if ($pins.ContainsKey($key)) { throw "Duplicate native Roslyn pin: $key" }
            $parts = @($Matches[2] -split '\.') + @('0', '0')
            $pins[$key] = $parts[0..2] -join '.'
        }
    }
    return $pins
}

function Assert-RoslynPins(
    [xml] $Central,
    [string] $ProducerDependencies,
    [string] $ProducerLock,
    [string] $TestDependencies,
    [string] $TestLock) {
    $sets = @(
        @{
            Name = 'producer'; Dependencies = $ProducerDependencies; Lock = $ProducerLock
            Requirements = @{
                'analyzers/Microsoft.CodeAnalysis.Common' = 'MicrosoftCodeAnalysisVersion'
                'analyzers/Microsoft.CodeAnalysis.CSharp.Workspaces' = 'MicrosoftCodeAnalysisVersion'
                'generator/Microsoft.CodeAnalysis.CSharp' = 'MicrosoftCodeAnalysisVersionForSourceGen'
            }
        },
        @{
            Name = 'tests'; Dependencies = $TestDependencies; Lock = $TestLock
            Requirements = @{
                'analyzers/Microsoft.CodeAnalysis' = 'MicrosoftCodeAnalysisVersionForTests'
                'analyzers/Microsoft.CodeAnalysis.Common' = 'MicrosoftCodeAnalysisVersionForTests'
                'analyzers/Microsoft.CodeAnalysis.CSharp.Workspaces' = 'MicrosoftCodeAnalysisVersionForTests'
            }
        }
    )
    foreach ($set in $sets) {
        $declared = Get-RoslynPins $set.Dependencies $false
        $locked = Get-RoslynPins $set.Lock $true
        foreach ($requirement in $set.Requirements.GetEnumerator()) {
            $property = @($Central.SelectNodes("/Project/PropertyGroup/$($requirement.Value)"))
            if ($property.Count -ne 1 -or $property[0].InnerText -notmatch '^\d+\.\d+\.\d+$') {
                throw "Expected one exact central Roslyn version: $($requirement.Value)"
            }
            $expected = $property[0].InnerText
            foreach ($pins in @($declared, $locked)) {
                if (-not $pins.ContainsKey($requirement.Key) -or $pins[$requirement.Key] -cne $expected) {
                    throw "Native Roslyn $($set.Name) pin $($requirement.Key) must match $($requirement.Value)=$expected. Update its paket.dependencies and regenerate paket.lock."
                }
            }
        }
    }
}

if ($SelfTest) {
    $central = '<Project><PropertyGroup><MicrosoftCodeAnalysisVersion>4.14.0</MicrosoftCodeAnalysisVersion><MicrosoftCodeAnalysisVersionForTests>4.14.0</MicrosoftCodeAnalysisVersionForTests><MicrosoftCodeAnalysisVersionForSourceGen>4.8.0</MicrosoftCodeAnalysisVersionForSourceGen></PropertyGroup></Project>'
    $producer = @'
group analyzers
    nuget Microsoft.CodeAnalysis.Common == 4.14.0
    nuget Microsoft.CodeAnalysis.CSharp.Workspaces == 4.14.0
group generator
    nuget Microsoft.CodeAnalysis.CSharp == 4.8.0
'@
    $producerLock = @'
GROUP analyzers
NUGET
    Microsoft.CodeAnalysis.Common (4.14)
    Microsoft.CodeAnalysis.CSharp.Workspaces (4.14)
GROUP generator
NUGET
    Microsoft.CodeAnalysis.CSharp (4.8)
'@
    $tests = @'
group analyzers
    nuget Microsoft.CodeAnalysis == 4.14.0
    nuget Microsoft.CodeAnalysis.Common == 4.14.0
    nuget Microsoft.CodeAnalysis.CSharp.Workspaces == 4.14.0
'@
    $testLock = @'
GROUP analyzers
NUGET
    Microsoft.CodeAnalysis (4.14)
    Microsoft.CodeAnalysis.Common (4.14)
    Microsoft.CodeAnalysis.CSharp.Workspaces (4.14)
'@
    $cases = @(
        @{ Name = 'AlignedPinsAcceptNormalizedLockVersions'; ExpectedError = ''; Change = @{} },
        @{ Name = 'RejectsOldProducerDeclaration'; ExpectedError = 'Native Roslyn producer pin'; Change = @{ ProducerDependencies = $producer.Replace('4.14.0', '3.11.0') } },
        @{ Name = 'RejectsOldProducerLock'; ExpectedError = 'Native Roslyn producer pin'; Change = @{ ProducerLock = $producerLock.Replace('(4.14)', '(3.11)') } },
        @{ Name = 'RejectsOldTestDeclaration'; ExpectedError = 'Native Roslyn tests pin'; Change = @{ TestDependencies = $tests.Replace('4.14.0', '4.10.0') } },
        @{ Name = 'RejectsOldTestLock'; ExpectedError = 'Native Roslyn tests pin'; Change = @{ TestLock = $testLock.Replace('(4.14)', '(4.10)') } },
        @{ Name = 'RejectsMissingGenerator'; ExpectedError = 'Native Roslyn producer pin'; Change = @{ ProducerLock = $producerLock.Replace('Microsoft.CodeAnalysis.CSharp (4.8)', 'Other.Package (4.8)') } },
        @{ Name = 'RejectsDuplicatePin'; ExpectedError = 'Duplicate native Roslyn pin'; Change = @{ TestDependencies = $tests + "`n    nuget Microsoft.CodeAnalysis == 4.14.0" } },
        @{ Name = 'RejectsChangedCentralVersion'; ExpectedError = 'Native Roslyn producer pin'; Change = @{ Central = $central.Replace('<MicrosoftCodeAnalysisVersion>4.14.0', '<MicrosoftCodeAnalysisVersion>4.15.0') } }
    )
    foreach ($case in $cases) {
        $input = @{
            Central = $central; ProducerDependencies = $producer; ProducerLock = $producerLock
            TestDependencies = $tests; TestLock = $testLock
        }
        foreach ($key in $case.Change.Keys) { $input[$key] = $case.Change[$key] }
        $failure = $null
        try { Assert-RoslynPins @input }
        catch { $failure = $_.Exception.Message }
        if (($case.ExpectedError -and (-not $failure -or -not $failure.Contains($case.ExpectedError))) -or
            (-not $case.ExpectedError -and $failure)) {
            throw "$($case.Name): unexpected validation outcome: $failure"
        }
        Write-Host "Passed $($case.Name)"
    }
    Write-Host "Roslyn dependency self-tests: $($cases.Count) passed."
}

Assert-RoslynPins `
    -Central (Get-Content -LiteralPath (Join-Path $root 'Directory.Packages.props') -Raw) `
    -ProducerDependencies (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'paket.dependencies') -Raw) `
    -ProducerLock (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'paket.lock') -Raw) `
    -TestDependencies (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tests-support\paket.dependencies') -Raw) `
    -TestLock (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'tests-support\paket.lock') -Raw)
Write-Host 'Native Roslyn declarations and lockfiles match the central analyzer versions.'
