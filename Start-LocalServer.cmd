@echo off
setlocal
set "SNOW_ROOT=%~dp0"
set "SNOW_SERVER=%SNOW_ROOT%Server"

if not exist "%SNOW_SERVER%\MikuSB.exe" (
  echo Local server files are missing.
  echo Run Setup-LocalWorkspace.ps1 first.
  pause
  exit /b 1
)

cd /d "%SNOW_SERVER%"
MikuSB.exe
