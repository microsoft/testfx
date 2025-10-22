$j = Start-Job { $global:PSScriptRoot = $using:PSScriptRoot; . $PSScriptRoot/StartDumpAsync.ps1 }

dotnet test --solution NonWindowsTests.slnf --no-build -bl:$PSScriptRoot/artifacts/TestResults/Debug/TestStep.binlog --no-progress -p:UsingDotNetTest=true

 $j | Receive-Job
