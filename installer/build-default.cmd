@echo off
setlocal
REM Default build wrapper: no arguments needed.
REM Adjust VERSION here for releases.
set CONFIG=Release
set RUNTIME=win-x64
set VERSION=1.0.0

call "%~dp0build.cmd" -Configuration %CONFIG% -Runtime %RUNTIME% -Version %VERSION%
exit /b %ERRORLEVEL%
