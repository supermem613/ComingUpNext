@echo off
setlocal enabledelayedexpansion

REM Wrapper batch to run build.ps1 without assuming current shell is PowerShell.
REM Usage examples:
REM   build.cmd                              (defaults: -Configuration Release -Runtime win-x64; version auto from csproj)
REM   build.cmd -Configuration Debug         (override configuration)
REM   build.cmd -Version 1.2.3               (override version without editing csproj)
REM   build.cmd -Install                     (build then uninstall previous and install newly built MSI)

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

REM Detect -Install switch (case-insensitive) and remove it from args passed to build.ps1.
set INSTALL_REQUESTED=false
set FILTERED_ARGS=
for %%A in (%*) do (
  if /I "%%~A"=="-Install" (
    set INSTALL_REQUESTED=true
  ) else (
    set FILTERED_ARGS=!FILTERED_ARGS! %%A
  )
)

REM Run PowerShell build with filtered args (to avoid unknown -Install parameter).
"%POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build.ps1" %DEFAULT_CONFIG% %DEFAULT_RUNTIME% %FILTERED_ARGS%
set EXITCODE=%ERRORLEVEL%
if %EXITCODE% NEQ 0 (
  echo Build script failed with exit code %EXITCODE%.
  exit /b %EXITCODE%
)

echo Build script completed successfully.

if /I "%INSTALL_REQUESTED%"=="true" call :doInstall

goto :eof

:doInstall
echo Install flag detected: performing uninstall of previous version (if any) and installing new MSI...
set "MSI_FILE="
for /f "delims=" %%F in ('dir /b /a:-d /o:-d "%SCRIPT_DIR%ComingUpNextTray-*.msi" 2^>nul') do if not defined MSI_FILE set "MSI_FILE=%SCRIPT_DIR%%%F"
if not defined MSI_FILE (
  echo ERROR: No MSI found to install.
  exit /b 1
)
echo Found MSI: %MSI_FILE%
"%POWERSHELL%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install.ps1" -MsiPath "%MSI_FILE%"
set EXITCODE=%ERRORLEVEL%
if %EXITCODE% NEQ 0 (
  echo Install.ps1 failed with exit code %EXITCODE%.
  exit /b %EXITCODE%
)
echo Installation phase completed.
goto :eof

exit /b 0
