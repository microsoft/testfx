Write-Host "Starting dotnet test"

Start-Job { $global:PSScriptRoot = $using:PSScriptRoot; dotnet test --solution NonWindowsTests.slnf --no-build -bl:$PSScriptRoot/artifacts/TestResults/Debug/TestStep.binlog --no-progress -p:UsingDotNetTest=true }

Write-Host "Started dotnet test"

Import-Module "$PSScriptRoot/Microsoft.Diagnostics.NETCore.Client.dll"

$dir = "$PSScriptRoot/dumps"

Start-Sleep -Duration ([TimeSpan]::FromMinutes(20))

Write-Host "Timedout!! Dumping now..."

New-Item -ItemType Directory -Path $dir -Force -ErrorAction Ignore

Write-Host "Getting processes..."

$processes = Get-Process -Name "Microsoft.Testing*","MSTest*" -ErrorAction Ignore

Write-Host "Got processes..."

if (-not $processes) {
    Set-Content -Path "$dir/output.txt" -Value "No dotnet processes found".
    Write-Host "No dotnet processes found."
}

foreach ($process in $processes) {
    $name = "$($process.Id)_$($process.Name)"

    Write-Host "Dumping $name"
    Set-Content -Path "$dir/$name.txt" -Value "Dumping $name"

    try {
        $client = [Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient]::new($process.Id);
        $fullPath =  $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath("$dir/$name.dmp")
        $client.WriteDump("Triage", $fullPath, $true);
        Write-Host "Dump written"
        try {
            $process.Kill()
        }
        catch {
            Add-Content -Path "$dir/$name.txt" -Value "$_"
        }
    }
    catch {
        Add-Content -Path "$dir/$name.txt" -Value "$_"
        Write-Host "ERR DUMPING!!"
    }
}
