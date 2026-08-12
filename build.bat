@echo off
REM ============================================================
REM  AutoPickup build script
REM  Output: ..\BepInEx\Plugins\AutoPickupMod.dll
REM ============================================================
setlocal
set GAME=%~dp0..
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERROR] csc.exe not found. Please install .NET Framework 4.x
    exit /b 1
)

"%CSC%" /nologo /t:library /codepage:65001 ^
 /out:"%GAME%\BepInEx\Plugins\AutoPickupMod.dll" ^
 /r:"%GAME%\BepInEx\core\BepInEx.dll" ^
 /r:"%GAME%\BepInEx\core\0Harmony.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\UnityEngine.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\UnityEngine.CoreModule.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\UnityEngine.InputLegacyModule.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\UnityEngine.PhysicsModule.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\netstandard.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\Assembly-CSharp.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\UnturnedDat.dll" ^
 /r:"%GAME%\Unturned_Data\Managed\SDG.Glazier.Runtime.dll" ^
 "%~dp0AutoPickup.cs"

if errorlevel 1 (
    echo [ERROR] Compilation failed. See messages above.
    exit /b 1
)

echo [OK] Built: %GAME%\BepInEx\Plugins\AutoPickupMod.dll
endlocal
