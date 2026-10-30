function Get-SampleRelativePath {
    param(
        [string]$FullPath,
        [string]$SamplesFolder
    )

    $samplesFolderWithTrailingSeparator = [System.IO.Path]::GetFullPath($SamplesFolder).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $normalizedFullPath = [System.IO.Path]::GetFullPath($FullPath)

    if (!$normalizedFullPath.StartsWith($samplesFolderWithTrailingSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$FullPath' is not under the public samples folder '$SamplesFolder'."
    }

    return $normalizedFullPath.Substring($samplesFolderWithTrailingSeparator.Length)
}

function Get-SampleBinlogArgument {
    param(
        [string]$BinaryLogDirectory,
        [string]$LogName,
        [string]$ArgumentPrefix = "-bl:"
    )

    $binlogPath = Join-Path $BinaryLogDirectory "$LogName.binlog"
    return "$ArgumentPrefix$binlogPath"
}

function Invoke-SampleProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory = $true)]
        [string]$LogFilePrefix,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $stdoutPath = "$LogFilePrefix.stdout.log"
    $stderrPath = "$LogFilePrefix.stderr.log"
    $null = [System.IO.Directory]::CreateDirectory((Split-Path -Parent $LogFilePrefix))

    # Start-Process joins ArgumentList rather than preserving native argument boundaries.
    $quotedArguments = @($Arguments | ForEach-Object {
        $argument = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        $argument = [regex]::Replace($argument, '(\\+)$', '$1$1')
        '"' + $argument + '"'
    })

    $startParameters = @{
        FilePath = $FilePath
        WorkingDirectory = (Get-Location).Path
        NoNewWindow = $true
        PassThru = $true
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
    }
    if ($quotedArguments.Count -ne 0) {
        $startParameters.ArgumentList = $quotedArguments -join " "
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process @startParameters
    try {
        # Windows PowerShell needs a retained handle to read ExitCode after the wait.
        $null = $process.Handle
        $lastProgress = [TimeSpan]::Zero
        while (!$process.WaitForExit(250)) {
            if (($stopwatch.Elapsed - $lastProgress).TotalSeconds -ge 60) {
                $lastProgress = $stopwatch.Elapsed
                Write-Host "RUNNING: $Description ($([int]$stopwatch.Elapsed.TotalSeconds)s). Transcripts: $LogFilePrefix.{stdout,stderr}.log"
            }
        }

        # The parameterless wait also drains asynchronous redirected output.
        $process.WaitForExit()
        $exitCode = $process.ExitCode
    }
    finally {
        if (!$process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }

        $process.Dispose()
        $stopwatch.Stop()
    }

    $duration = $stopwatch.Elapsed.TotalSeconds.ToString("F1", [System.Globalization.CultureInfo]::InvariantCulture)
    $status = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
    Write-Host "${status}: $Description (${duration}s, exit $exitCode). Transcripts: $LogFilePrefix.{stdout,stderr}.log"
    if ($exitCode -ne 0) {
        Write-Host "--- stdout: $stdoutPath ---"
        Get-Content -LiteralPath $stdoutPath | Write-Host
        Write-Host "--- stderr: $stderrPath ---"
        Get-Content -LiteralPath $stderrPath | Write-Host
    }

    $global:LASTEXITCODE = $exitCode
    return [pscustomobject]@{
        ExitCode = $exitCode
        StandardOutputPath = $stdoutPath
        StandardErrorPath = $stderrPath
    }
}
