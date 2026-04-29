@echo off
REM Helper: run elevated install (interactive Tx/Rx prompt)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1" %*
