# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

function Write-Log ([string] $message, $messageColor = "Green") {
    $currentColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = $messageColor
    if ($message) {
      Write-Output "... $message"
    }
    $Host.UI.RawUI.ForegroundColor = $currentColor
}

Push-Location
try {
    Write-Log "Downloading the Windows SDK 10.0.14393.795..."
    Invoke-WebRequest -Method Get -Uri https://go.microsoft.com/fwlink/p/?LinkId=838916 -OutFile sdksetup.exe -UseBasicParsing

    Write-Log "Installing the Windows SDK, setup might request elevation please approve."
    $process = Start-Process -Wait sdksetup.exe -ArgumentList "/quiet", "/norestart", "/ceip off", "/features OptionId.WindowsSoftwareDevelopmentKit"  -PassThru
    Remove-Item sdksetup.exe -Force

    if($process.ExitCode -eq 0) 
    {
        Write-Log "Done"
    }
    else 
    {
        Write-Log "Failed (Exit code: $($process.ExitCode))" -messageColor "Red"
    }
}
finally {
    Pop-Location
}

