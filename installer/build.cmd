@echo off
setlocal enabledelayedexpansion

REM Wrapper batch to run build.ps1 without assuming current shell is PowerShell.
REM Usage examples:
REM   build.cmd                      (defaults: -Configuration Release -Runtime win-x64; version auto from csproj)
REM   build.cmd -Configuration Debug (override configuration)
REM   build.cmd -Version 1.2.3       (override version without editing csproj)

REM Prefer pwsh if available, fallback to Windows PowerShell.
set POWERSHELL=pwsh.exe
where %POWERSHELL% >nul 2>&1
if errorlevel 1 (
  set POWERSHELL=powershell.exe
)

REM Resolve script directory (folder of this cmd)
set SCRIPT_DIR=%~dp0

REM If no -Configuration provided, add default Release.
set CONFIG_PROVIDED=false
for %%A in (%*) do (
  if /I "%%~A"=="-Configuration" set CONFIG_PROVIDED=true
)
if /I "%CONFIG_PROVIDED%"=="false" set DEFAULT_CONFIG=-Configuration Release

REM If no -Runtime provided, add default win-x64.
set RUNTIME_PROVIDED=false
for %%A in (%*) do (
  if /I "%%~A"=="-Runtime" set RUNTIME_PROVIDED=true
)
if /I "%RUNTIME_PROVIDED%"=="false" set DEFAULT_RUNTIME=-Runtime win-x64

"%POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1" %DEFAULT_CONFIG% %DEFAULT_RUNTIME% %*
set EXITCODE=%ERRORLEVEL%
if %EXITCODE% NEQ 0 (
  echo Build script failed with exit code %EXITCODE%.
  exit /b %EXITCODE%
)

echo Build script completed successfully.
exit /b 0
