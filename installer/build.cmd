@echo off
setlocal enabledelayedexpansion

REM Wrapper batch to run build.ps1 without assuming current shell is PowerShell.
REM Usage: build.cmd -Configuration Release -Runtime win-x64 -Version 1.0.0

REM Prefer pwsh if available, fallback to Windows PowerShell.
set POWERSHELL=pwsh.exe
where %POWERSHELL% >nul 2>&1
if errorlevel 1 (
  set POWERSHELL=powershell.exe
)

REM Resolve script directory (folder of this cmd)
set SCRIPT_DIR=%~dp0

"%POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1" %*
set EXITCODE=%ERRORLEVEL%
if %EXITCODE% NEQ 0 (
  echo Build script failed with exit code %EXITCODE%.
  exit /b %EXITCODE%
)

echo Build script completed successfully.
exit /b 0
