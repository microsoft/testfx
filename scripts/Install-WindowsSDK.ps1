# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

Write-Host "Downloading Windows SDK 10.0.14393.795..." -ForegroundColor Green
Invoke-WebRequest -Method Get -Uri https://go.microsoft.com/fwlink/p/?LinkId=838916 -OutFile sdksetup.exe -UseBasicParsing

Write-Host "Installing Windows SDK, if setup requests elevation please approve." -ForegroundColor Green
$process = Start-Process -Wait sdksetup.exe -ArgumentList "/quiet", "/norestart", "/ceip off", "/features OptionId.WindowsSoftwareDevelopmentKit"  -PassThru
Remove-Item sdksetup.exe -Force

if($process.ExitCode -eq 0)
{
    Write-Host "Done" -ForegroundColor Green
}
else
{
    Write-Error "Failed to install Windows SDK (Exit code: $($process.ExitCode))"
}
