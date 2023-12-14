# Avoid automatic attach(AeDebug)

Create a file called `Crash.reg` with the following contents:

```txt
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AeDebug\AutoExclusionList]
"UnhandledExceptionPolicyTests.exe"=dword:00000001


```

Install the registry file by double clicking on it.
