@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\build.ps1""" -installWindowsSdk -restore -build %*; exit $LASTEXITCODE"
exit /b %ErrorLevel%
