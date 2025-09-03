@echo off
setlocal

REM WinRelay Build Script Wrapper
REM This batch file provides easy access to the PowerShell build script

echo === WinRelay Build Wrapper ===
echo.

REM Check if PowerShell is available
powershell -Command "exit 0" >nul 2>&1
if errorlevel 1 (
    echo Error: PowerShell is not available.
    echo Please ensure PowerShell is installed and in your PATH.
    pause
    exit /b 1
)

REM Default action if no arguments provided
if "%~1"=="" (
    echo Usage: build.bat [clean^|build^|publish^|release^|all^|help]
    echo.
    echo Commands:
    echo   clean    - Clean build artifacts
    echo   build    - Build the project (Debug)
    echo   publish  - Publish as single-file executable (Debug)
    echo   release  - Build and publish in Release mode
    echo   all      - Clean, build, and publish in Release mode
    echo   help     - Show detailed help
    echo.
    pause
    exit /b 0
)

REM Parse command line arguments and call PowerShell script
if /i "%1"=="clean" (
    powershell -ExecutionPolicy Bypass -File "build.ps1" -Clean
) else if /i "%1"=="build" (
    powershell -ExecutionPolicy Bypass -File "build.ps1" -Build
) else if /i "%1"=="publish" (
    powershell -ExecutionPolicy Bypass -File "build.ps1" -Publish
) else if /i "%1"=="release" (
    powershell -ExecutionPolicy Bypass -File "build.ps1" -Build -Publish -Release
) else if /i "%1"=="all" (
    powershell -ExecutionPolicy Bypass -File "build.ps1" -Clean -Build -Publish -Release
) else if /i "%1"=="installer" (
    powershell -ExecutionPolicy Bypass -File "build.ps1" -Clean -Publish -Release -CreateInstaller
) else if /i "%1"=="help" (
    powershell -ExecutionPolicy Bypass -File "build.ps1"
) else (
    echo Unknown command: %1
    echo Use 'build.bat help' for detailed usage information.
    pause
    exit /b 1
)

REM Check for errors
if errorlevel 1 (
    echo.
    echo Build failed with error code %errorlevel%
    pause
    exit /b %errorlevel%
)

echo.
echo Build completed successfully.
pause