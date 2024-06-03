param(
    [switch]$Force
)

function Download {
    <# 
 .SYNOPSIS 
     Downloads a given uri and saves it to outputFile
 .DESCRIPTION
     Downloads a given uri and saves it to outputFile
 PARAMETER uri
    The uri to fetch
.PARAMETER outputFile
    The outputh file path to save the uri
#>
    param(
        [Parameter(Mandatory = $true)]
        $uri,

        [Parameter(Mandatory = $true)]
        $outputFile
    )
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = 'SilentlyContinue' # Don't display the console progress UI - it's a huge perf hit

    $maxRetries = 5
    $retries = 1

    while ($true) {
        try {
            Write-Host "GET $uri"
            Invoke-WebRequest $uri -OutFile $outputFile
            break
        }
        catch {
            Write-Host "Failed to download '$uri'"
            $error = $_.Exception.Message
        }

        if (++$retries -le $maxRetries) {
            Write-Warning $error -ErrorAction Continue
            $delayInSeconds = [math]::Pow(2, $retries) - 1 # Exponential backoff
            Write-Host "Retrying. Waiting for $delayInSeconds seconds before next attempt ($retries of $maxRetries)."
            Start-Sleep -Seconds $delayInSeconds
        }
        else {
            Write-Error $error -ErrorAction Continue
            throw "Unable to download file in $maxRetries attempts."
        }
    }

    Write-Host "Download of '$uri' complete, saved to $outputFile..."

}

function Install-Procdump {
    <#
.SYNOPSIS
    Installs ProcDump into a folder in this repo.
.DESCRIPTION
    This script downloads and extracts the ProcDump.
.PARAMETER Force
    Overwrite the existing installation
#>
    param(
        [switch]$Force
    )
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue' # Workaround PowerShell/PowerShell#2138

    Set-StrictMode -Version 1

    $repoRoot = Resolve-Path "$PSScriptRoot\..\.."
    $installDir = "$repoRoot\.tools\ProcDump\"

    if (Test-Path "$installDir\procdump.exe") {
        if ($Force) {
            Remove-Item -Force -Recurse $installDir
        }
        else {
            Write-Host "ProcDump already installed to $installDir. Exiting without action. Call this script again with -Force to overwrite."
            exit 0
        }
    }

    mkdir $installDir -ea Ignore | out-null
    Write-Host "Starting ProcDump download"
    Download "https://download.sysinternals.com/files/Procdump.zip" "$installDir/ProcDump.zip"
    Write-Host "Done downloading ProcDump"
    Expand-Archive "$installDir/ProcDump.zip" -d "$installDir"
    Write-Host "Expanded ProcDump to $installDir"

    if ($env:TF_BUILD) {
        Write-Host "##vso[task.setvariable variable=PROCDUMP_PATH]$installDir"
        Write-Host "##vso[task.prependpath]$installDir"
    }
}

Install-Procdump -Force:$Force