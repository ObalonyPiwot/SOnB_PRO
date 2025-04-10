@echo off
cd /d %~dp0

for /f "tokens=*" %%a in ('powershell -command "(Get-Content config.json | ConvertFrom-Json).ExpectedClients"') do set CLIENT_COUNT=%%a

start "SERVER" cmd /k dotnet run -- server
timeout /t 1 >nul

setlocal enabledelayedexpansion
for /l %%i in (1,1,%CLIENT_COUNT%) do (
    start "CLIENT %%i" cmd /k dotnet run -- client %%i
    timeout /t 1 >nul
)
endlocal

exit
