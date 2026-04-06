@echo off
setlocal enabledelayedexpansion
echo =======================================================
echo      DIAGNOSTIC REALIS - Enhanced Edition
echo =======================================================
echo.
echo Analyse des causes de crash avec Enhanced Edition...
echo.

REM Couleurs pour PowerShell
set "GREEN=[32m"
set "RED=[31m"  
set "YELLOW=[33m"
set "NC=[0m"

echo 1. VERIFICATION BATTLYE
echo ----------------------------------------
set "BATTLYE_FOUND=0"
if exist "C:\Program Files\Epic Games\GTAV\BattlEye" (
    echo %RED%[!] BattlEye DETECTE - DOIT etre desactive%NC%
    set "BATTLYE_FOUND=1"
) else (
    echo %GREEN%[OK] BattlEye non detecte ou desactive%NC%
)
echo.

echo 2. VERIFICATION SCRIPT HOOK V
echo ----------------------------------------
if exist "C:\Program Files\Epic Games\GTAV\ScriptHookV.dll" (
    echo %GREEN%[OK] Script Hook V detecte%NC%
) else (
    echo %RED%[!] Script Hook V MANQUANT - OBLIGATOIRE%NC%
    echo    Telecharger: http://dev-c.com/gtav/scripthookv/
)

if exist "C:\Program Files\Epic Games\GTAV\asiloader.dll" (
    echo %GREEN%[OK] ASI Loader detecte%NC%
) else (
    echo %RED%[!] ASI Loader MANQUANT%NC%
)
echo.

echo 3. VERIFICATION SCRIPTHOOKVDOTNET
echo ----------------------------------------
if exist "C:\Program Files\Epic Games\GTAV\ScriptHookVDotNet.asi" (
    echo %GREEN%[OK] ScriptHookVDotNet.asi detecte%NC%
) else (
    echo %RED%[!] ScriptHookVDotNet.asi MANQUANT%NC%
)

if exist "C:\Program Files\Epic Games\GTAV\ScriptHookVDotNet3.dll" (
    echo %GREEN%[OK] ScriptHookVDotNet3.dll detecte%NC%
) else (
    echo %RED%[!] ScriptHookVDotNet3.dll MANQUANT%NC%
)
echo.

echo 4. VERIFICATION VISUAL C++ REDISTRIBUTABLES
echo ----------------------------------------
reg query "HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" >nul 2>&1
if %errorlevel% == 0 (
    echo %GREEN%[OK] Visual C++ 2015-2022 x64 installe%NC%
) else (
    echo %RED%[!] Visual C++ 2015-2022 x64 MANQUANT%NC%
    echo    Telecharger depuis Microsoft.com
)
echo.

echo 5. VERIFICATION DOSSIER SCRIPTS
echo ----------------------------------------
if exist "C:\Program Files\Epic Games\GTAV\scripts" (
    echo %GREEN%[OK] Dossier scripts existe%NC%
    
    REM Compter les fichiers DLL
    set "DLL_COUNT=0"
    for %%f in ("C:\Program Files\Epic Games\GTAV\scripts\*.dll") do (
        set /a DLL_COUNT=!DLL_COUNT!+1
        echo    - %%~nxf
    )
    echo    Total DLL: !DLL_COUNT!
) else (
    echo %YELLOW%[INFO] Dossier scripts inexistant%NC%
)
echo.

echo 6. VERIFICATION FICHIERS REALIS
echo ----------------------------------------
if exist "bin\net48\REALIS.Loader.dll" (
    echo %GREEN%[OK] REALIS compile dans bin\net48\%NC%
) else (
    echo %RED%[!] REALIS non compile - executer dotnet build%NC%
)
echo.

echo 7. VERIFICATION LOGS PRECEDENTS
echo ----------------------------------------
if exist "C:\Program Files\Epic Games\GTAV\ScriptHookVDotNet.log" (
    echo %GREEN%[OK] Log ScriptHookVDotNet trouve%NC%
    echo Derniere modification:
    forfiles /p "C:\Program Files\Epic Games\GTAV" /m "ScriptHookVDotNet.log" /c "cmd /c echo @fdate @ftime" 2>nul
    
    REM Rechercher l'erreur TypeInitializationException
    findstr /c:"TypeInitializationException" "C:\Program Files\Epic Games\GTAV\ScriptHookVDotNet.log" >nul 2>&1
    if !errorlevel! == 0 (
        echo %RED%[!] ERREUR DETECTEE: TypeInitializationException%NC%
        echo    C'est l'erreur GitHub #1451 - NativeMemory crash
    )
) else (
    echo %YELLOW%[INFO] Pas de log ScriptHookVDotNet precedent%NC%
)
echo.

echo 8. TESTS AVANCES
echo ----------------------------------------

REM Test d'ecriture dans le dossier GTA V
echo Test permissions d'ecriture...
echo test > "C:\Program Files\Epic Games\GTAV\test_write.tmp" 2>nul
if exist "C:\Program Files\Epic Games\GTAV\test_write.tmp" (
    echo %GREEN%[OK] Permissions d'ecriture OK%NC%
    del "C:\Program Files\Epic Games\GTAV\test_write.tmp" >nul 2>&1
) else (
    echo %RED%[!] PERMISSIONS INSUFFISANTES - Executer en Admin%NC%
)

REM Verification version GTA V
echo Verification version GTA V...
if exist "C:\Program Files\Epic Games\GTAV\GTA5.exe" (
    for /f "usebackq delims=" %%v in (`powershell "(Get-Item 'C:\Program Files\Epic Games\GTAV\GTA5.exe').VersionInfo.FileVersion"`) do (
        echo Version GTA V: %%v
        
        REM Versions problematiques connues
        echo %%v | findstr "1.0.325" >nul
        if !errorlevel! == 0 (
            echo %RED%[!] Version Enhanced Edition - Requires nightly SHVDN%NC%
        )
    )
) else (
    echo %RED%[!] GTA5.exe introuvable%NC%
)
echo.

echo =======================================================
echo                   RESUME DIAGNOSTIC
echo =======================================================

echo SOLUTIONS RECOMMANDEES:
echo.
echo 1. Si BattlEye detecte:
echo    - Rockstar Launcher ^> Settings ^> Decocher BattlEye
echo.
echo 2. Si Script Hook V manquant:
echo    - Telecharger: http://dev-c.com/gtav/scripthookv/
echo    - Copier ScriptHookV.dll ET asiloader.dll
echo.  
echo 3. Si Visual C++ manquant:
echo    - Installer: Microsoft Visual C++ 2015-2022 x64
echo.
echo 4. Si permissions insuffisantes:
echo    - Clic droit sur ce script ^> Executer en tant qu'admin
echo.
echo 5. Pour tester sans REALIS:
echo    - Supprimer temporairement le dossier scripts/
echo    - Lancer GTA V
echo    - Appuyer F4 pour tester la console SHVDN
echo.
echo 6. Si l'erreur TypeInitializationException persiste:
echo    - Reinstaller completement ScriptHookVDotNet nightly
echo    - Verifier qu'aucun autre ASI n'interfere
echo.

echo =======================================================
echo Appuyez sur une touche pour fermer...
pause >nul 