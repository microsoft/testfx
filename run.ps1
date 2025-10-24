Write-Host "Starting dotnet test"

if ($env:_BuildConfig -eq 'Debug') {
    Start-Job { $global:PSScriptRoot = $using:PSScriptRoot; dotnet test --solution NonWindowsTests.slnf --no-build -bl:$PSScriptRoot/artifacts/TestResults/Debug/TestStep.binlog --no-progress -p:UsingDotNetTest=true }
} else {
    dotnet test --solution NonWindowsTests.slnf --no-build --no-progress -p:UsingDotNetTest=true
}

Write-Host "Started dotnet test"

# Import-Module "$PSScriptRoot/Microsoft.Diagnostics.NETCore.Client.dll"

# $dir = "$PSScriptRoot/dumps"

dotnet tool install --global dotnet-stack

Start-Sleep -Duration ([TimeSpan]::FromMinutes(20))

Write-Host "Timedout!! Dumping now..."

# New-Item -ItemType Directory -Path $dir -Force -ErrorAction Ignore

Write-Host "Getting processes..."

$processes = Get-Process -Name "Microsoft.Testing*","MSTest*","dotnet" -ErrorAction Ignore

Write-Host "Got processes..."
Write-Host $processes

ps -eo pid,command

# dotnet stack ps

if (-not $processes) {
    Write-Host "No processes found."
}

Write-Host "Iterating."

foreach ($process in $processes) {
    $name = "$($process.Id)_$($process.Name)"

    Write-Host "Dumping $name"

    # dotnet stack report --process-id $process.Id
    try {
        # $client = [Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient]::new($process.Id);
        # $fullPath =  $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath("$dir/$name.dmp")
        # $client.WriteDump("Triage", $fullPath, $true);
        # Write-Host "Dump written"
        try {
            $process.Kill()
        }
        catch {
            Write-Host "$_"
        }
    }
    catch {
        Write-Host "$_"
    }
}
