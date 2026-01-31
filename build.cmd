@echo off
:: Build script for Legion + LOQ Control
:: Creates a release build in the 'publish' folder

echo ========================================
echo Legion + LOQ Control - Release Build
echo ========================================
echo.

:: Check for .NET SDK
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: .NET SDK not found. Please install .NET 9 SDK.
    exit /b 1
)

:: Clean previous builds
echo Cleaning previous builds...
if exist "publish" rmdir /s /q "publish"

:: Restore packages
echo Restoring packages...
dotnet restore LegionLoqControl.sln
if errorlevel 1 (
    echo ERROR: Package restore failed.
    exit /b 1
)

:: Build release
echo Building release...
dotnet publish LegionLoqControl/LegionLoqControl.csproj -c Release -o publish --self-contained false
if errorlevel 1 (
    echo ERROR: Build failed.
    exit /b 1
)

echo.
echo ========================================
echo Build successful!
echo Output: publish\LegionLoqControl.exe
echo ========================================
pause
