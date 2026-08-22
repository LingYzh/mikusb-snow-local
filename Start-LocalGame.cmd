@echo off
setlocal
set "SNOW_ROOT=%~dp0"
set "SNOW_SERVER=%SNOW_ROOT%Server"

if not exist "%SNOW_SERVER%\SnowLauncher.exe" (
  echo Local client launcher files are missing.
  echo Run Setup-LocalWorkspace.ps1 first.
  pause
  exit /b 1
)

cd /d "%SNOW_SERVER%"
SnowLauncher.exe
