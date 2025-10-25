@echo off
REM Gravedigger VSS Verification Script
REM This script checks if Volume Shadow Copy Service is available and configured

echo ============================================================
echo   Gravedigger - VSS Verification Script
echo ============================================================
echo.

echo [1/6] Checking Windows version...
systeminfo | findstr /B /C:"OS Name" /C:"OS Version"
echo.

echo [2/6] Checking if VSS service is installed...
sc query VSS >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] VSS service is installed
) else (
    echo [ERROR] VSS service not found!
    echo VSS is not available on this system.
    goto :end
)
echo.

echo [3/6] Checking VSS service status...
sc query VSS | findstr "STATE"
sc query VSS | findstr "RUNNING" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo [OK] VSS service is running
) else (
    echo [WARNING] VSS service is not running
    echo Attempting to start VSS service...
    net start VSS
    if %ERRORLEVEL% EQU 0 (
        echo [OK] VSS service started successfully
    ) else (
        echo [ERROR] Failed to start VSS service
        echo You may need to run this script as Administrator
    )
)
echo.

echo [4/6] Checking Shadow Copy Provider service...
sc query swprv >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    sc query swprv | findstr "STATE"
    sc query swprv | findstr "RUNNING" >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo [OK] Shadow Copy Provider is running
    ) else (
        echo [WARNING] Shadow Copy Provider is not running
        echo Attempting to start...
        net start swprv
    )
) else (
    echo [WARNING] Shadow Copy Provider service not found (may not be required)
)
echo.

echo [5/6] Checking VSS providers...
vssadmin list providers
if %ERRORLEVEL% EQU 0 (
    echo [OK] VSS providers are available
) else (
    echo [ERROR] Could not list VSS providers
    echo VSS may not be properly installed
)
echo.

echo [6/6] Checking for existing shadow copies on C: drive...
vssadmin list shadows /for=C:
if %ERRORLEVEL% EQU 0 (
    echo.
    echo [INFO] Shadow copy check completed
) else (
    echo.
    echo [WARNING] No shadow copies found or error accessing them
)
echo.

echo ============================================================
echo   VSS Verification Summary
echo ============================================================
echo.
echo If you see errors above, Gravedigger may not work properly.
echo.
echo Common issues:
echo   - VSS service not running: Start it with 'net start VSS'
echo   - No shadow copies: Enable System Protection on your drive
echo   - Access denied: Run this script as Administrator
echo.
echo To enable System Protection (creates shadow copies):
echo   1. Right-click 'This PC' or 'Computer'
echo   2. Click 'Properties'
echo   3. Click 'System Protection' on the left
echo   4. Select your drive (C:) and click 'Configure'
echo   5. Select 'Turn on system protection'
echo   6. Click 'OK'
echo.
echo To manually create a shadow copy for testing:
echo   wmic shadowcopy call create Volume=C:\
echo.
echo ============================================================

:end
pause
