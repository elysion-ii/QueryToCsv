@echo off
setlocal

:menu
cls
echo ================================================
echo   QueryToCsv Build Menu
echo ================================================
echo.
echo   1. Build                        (net10.0)
echo   2. Installer                    (1 required)
echo   3. Full Build                   (1-2 sequential)
echo.
echo   9. Exit
echo.
echo ================================================
echo.

set "choice="
set /p choice="Select option (1-9): "

if "%choice%"=="1" goto build
if "%choice%"=="2" goto installer
if "%choice%"=="3" goto full_build
if "%choice%"=="9" goto exit
echo.
echo [ERROR] Invalid selection
timeout /t 2 > nul
goto menu

:build
echo.
echo ================================================
echo   Building QueryToCsv...
echo ================================================
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] QueryToCsv build failed
    pause
    goto menu
)
echo.
echo [SUCCESS] QueryToCsv build completed
echo Output: build\QueryToCsv\
pause
goto menu

:installer
echo.
echo ================================================
echo   Creating Installer...
echo ================================================
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0Installer.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Installer creation failed
    pause
    goto menu
)
echo.
echo [SUCCESS] Installer created
pause
goto menu

:full_build
echo.
echo ================================================
echo   Full Build: Build + Installer
echo ================================================
echo.

echo [1/2] Building QueryToCsv...
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] QueryToCsv build failed
    pause
    goto menu
)
echo.
echo [SUCCESS] QueryToCsv build completed
echo.

echo [2/2] Creating Installer...
echo.
powershell -ExecutionPolicy Bypass -File "%~dp0Installer.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Installer creation failed
    pause
    goto menu
)

echo.
echo ================================================
echo   Full Build Completed
echo ================================================
echo.
echo [SUCCESS] All build and installer tasks completed
echo Output: build\Installer\
pause
goto menu

:exit
echo.
echo Exiting...
exit /b 0
