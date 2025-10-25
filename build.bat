@echo off
REM Gravedigger Build Script
REM Builds the Gravedigger executable for Windows

echo ============================================
echo    Gravedigger Build Script
echo ============================================
echo.

REM Check if dotnet is available
where dotnet >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Using .NET SDK to build...
    dotnet build -c Release
    if %ERRORLEVEL% EQU 0 (
        echo.
        echo ============================================
        echo    Build Successful!
        echo    Output: bin\Release\net48\win-x64\
        echo ============================================
    ) else (
        echo.
        echo Build failed!
        exit /b 1
    )
) else (
    echo .NET SDK not found. Trying MSBuild...

    REM Try to find MSBuild
    set MSBUILD="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    if not exist %MSBUILD% (
        set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    if not exist %MSBUILD% (
        set MSBUILD="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    )

    if exist %MSBUILD% (
        echo Using MSBuild to build...
        %MSBUILD% Gravedigger.csproj /p:Configuration=Release
        if %ERRORLEVEL% EQU 0 (
            echo.
            echo ============================================
            echo    Build Successful!
            echo    Output: bin\Release\net48\
            echo ============================================
        ) else (
            echo.
            echo Build failed!
            exit /b 1
        )
    ) else (
        echo.
        echo ERROR: Neither .NET SDK nor MSBuild found!
        echo Please install:
        echo   - .NET SDK: https://dotnet.microsoft.com/download
        echo   - Or Visual Studio: https://visualstudio.microsoft.com/
        exit /b 1
    )
)

echo.
echo To run Gravedigger:
echo   1. Copy bin\Release\net48\win-x64\Gravedigger.exe to your desired location
echo   2. Create a config file: Gravedigger.exe --create-config
echo   3. Edit the config file with your settings
echo   4. Run: Gravedigger.exe
echo.
pause
