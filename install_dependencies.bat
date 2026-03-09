@echo off
title TransFile - Install Dependencies
echo =======================================================
echo Preparing TransFile Workspace...
echo =======================================================
echo.

:: 1. Verificar se o .NET 10 está instalado
echo [1/3] Checking .NET SDK installation...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK is not installed or not in your system PATH.
    echo Please install the .NET 10.0 SDK from https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)
for /f "tokens=1,2,3 delims=." %%a in ('dotnet --version') do (
    if %%a LSS 10 (
        echo WARNING: You seem to be running an older version of .NET ^(%%a.%%b.%%c^).
        echo TransFile recommends .NET 10.0 or higher.
        echo.
    ) else (
        echo .NET SDK found!
    )
)
echo.

:: 2. Instalar Workloads do MAUI (Android, Windows, etc.)
echo [2/3] Installing and restoring .NET Workloads (MAUI, Android, Windows)...
echo This might take a few minutes if it's the first time...
dotnet workload restore TransFile.sln
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore workloads. Make sure you are running this as Administrator.
    pause
    exit /b 1
)
echo.

:: 3. Restaurar pacotes NuGet
echo [3/3] Restoring NuGet Packages (ZXing, QRCoder, Toolkit)...
dotnet restore TransFile.sln
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore NuGet packages. Check your internet connection.
    pause
    exit /b 1
)
echo.

echo =======================================================
echo SUCCESS: All dependencies are installed!
echo You can now open TransFile.sln in Visual Studio or run 'dotnet run'.
echo =======================================================
pause