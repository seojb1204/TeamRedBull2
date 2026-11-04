@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Windows.ps1"
if errorlevel 1 (
  echo Build failed. Open build.log for details.
) else (
  echo Build complete. Open the Windows folder next to this project.
)
pause
