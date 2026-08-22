@echo off
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
if errorlevel 1 (
  echo vcvars64 failed
  exit /b 1
)
cd /d "%~dp0"
cl /nologo /LD /O2 /EHsc /Fe:MikuSB-Patch.dll patch_main.cpp ws2_32.lib mswsock.lib psapi.lib user32.lib
exit /b %ERRORLEVEL%
