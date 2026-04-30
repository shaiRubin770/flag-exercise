@echo off
REM ================================================================
REM  FlagExercise Uninstaller
REM  Double-click this file to remove the T(x) or R(x) service.
REM  Administrator rights are required - a UAC prompt will appear.
REM ================================================================

REM Check for administrator rights; if missing, re-launch elevated.
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting administrator privileges - please click Yes in the UAC prompt...
    powershell -NoProfile -Command "Start-Process -FilePath 'cmd.exe' -ArgumentList '/c \"%~f0\" %*' -Verb RunAs"
    exit /b
)

echo.
echo  FlagExercise Uninstaller
echo  ------------------------
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1" %*

echo.
pause
