@echo off
cd /d %~dp0

set /p CLIENT_COUNT=Client count: 

echo.
start "SERVER" cmd /k dotnet run -- server
timeout /t 1 >nul

setlocal enabledelayedexpansion
for /l %%i in (1,1,%CLIENT_COUNT%) do (
    start "CLIENT %%i" cmd /k dotnet run -- client %%i
    timeout /t 1 >nul
)
endlocal

exit