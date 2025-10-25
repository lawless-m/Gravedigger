@echo off
REM Gravedigger Build Script
REM Builds the Gravedigger executable for Windows using .NET 8

echo ============================================
echo    Gravedigger Build Script
echo    Target: .NET 8.0 (Windows)
echo ============================================
echo.

REM Check if dotnet is available
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: .NET SDK not found!
    echo.
    echo Please install .NET 8.0 SDK or later from:
    echo   https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

REM Check .NET version
echo Checking .NET SDK version...
dotnet --version
echo.

echo Building Gravedigger (Release)...
dotnet build -c Release

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo    Build Successful!
    echo ============================================
    echo.
    echo Building single-file executable...
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

    if %ERRORLEVEL% EQU 0 (
        echo.
        echo ============================================
        echo    Publish Successful!
        echo    Output: bin\Release\net8.0-windows\win-x64\publish\Gravedigger.exe
        echo ============================================
        echo.
        echo To run Gravedigger:
        echo   1. Navigate to: bin\Release\net8.0-windows\win-x64\publish\
        echo   2. Run: Gravedigger.exe --create-config
        echo   3. Edit gravedigger.config with your settings
        echo   4. Run: Gravedigger.exe
        echo.
        echo The executable is self-contained and includes all dependencies.
        echo No .NET runtime installation required on target machines!
    ) else (
        echo.
        echo Publish failed!
        exit /b 1
    )
) else (
    echo.
    echo Build failed!
    exit /b 1
)

echo.
pause
