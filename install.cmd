@echo off
setlocal

set "INSTALL_SCRIPT_URL=https://nolanblew.blob.core.windows.net/git-prune/install.ps1"
set "INSTALL_SCRIPT=%TEMP%\gitprune-install-%RANDOM%%RANDOM%.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -UseBasicParsing '%INSTALL_SCRIPT_URL%' -OutFile '%INSTALL_SCRIPT%'"
if errorlevel 1 (
    echo Failed to download the Git Prune installer.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_SCRIPT%" %*
set "EXIT_CODE=%ERRORLEVEL%"

del "%INSTALL_SCRIPT%" >nul 2>nul
exit /b %EXIT_CODE%
