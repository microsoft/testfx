if (Test-Path "${env:ProgramFiles(x86)}\Windows Kits\10\UnionMetadata\10.0.16299.0") {
    Write-Host "Windows SDK 10.0.16299 is already installed, skipping..."
} else {
    Write-Host "Downloading Windows SDK 10.0.16299..."
    Invoke-WebRequest -Method Get -Uri https://go.microsoft.com/fwlink/p/?linkid=864422 -OutFile sdksetup.exe -UseBasicParsing

    Write-Host "Installing Windows SDK, if setup requests elevation please approve." -ForegroundColor Green
    $process = Start-Process -Wait sdksetup.exe -ArgumentList "/quiet", "/norestart", "/ceip off", "/features OptionId.UWPManaged"  -PassThru

    if ($process.ExitCode -eq 0) {
        Remove-Item sdksetup.exe -Force
        Write-Host "Installation succeeded"
    }
    else {
        Write-Error "Failed to install Windows SDK (Exit code: $($process.ExitCode))"
    }
}
