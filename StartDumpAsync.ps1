param($Delay = 30)

Import-Module "$PSScriptRoot/Microsoft.Diagnostics.NETCore.Client.dll"


$dir = "$PSScriptRoot/dumps"


$processes = Get-Process -Name "dotnet" -ErrorAction Ignore

if (-not $processes) {
    Set-Content -Path "$dir/output.txt" -Value "No dotnet processes found".
}

Start-Sleep -Duration ([TimeSpan]::FromMinutes($Delay))

New-Item -ItemType Directory -Path $dir -Force -ErrorAction Ignore
foreach ($process in $processes) {
    $name = "$($process.Id)_$($process.Name)"

    Set-Content -Path "$dir/$name.txt" -Value "Dumping $name"

    try {
        $client = [Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient]::new($process.Id);
        $fullPath =  $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath("$dir/$name.dmp")
        $client.WriteDump("Normal", $fullPath);

        try {
            $process.Kill()
        }
        catch {
            Add-Content -Path "$dir/$name.txt" -Value "$_"
        }
    }
    catch {
        Add-Content -Path "$dir/$name.txt" -Value "$_"
    }
}




