@echo off
echo =======================================================
echo         Installation REALIS vers GTA V
echo                Enhanced Edition Ready
echo =======================================================
echo.

REM Modifiez ce chemin vers votre dossier GTA V
set "GTA_PATH=C:\Program Files\Epic Games\GTAV"

REM Vérification si le dossier GTA V existe
if not exist "%GTA_PATH%\GTA5.exe" (
    echo ERREUR: GTA5.exe non trouvé dans %GTA_PATH%
    echo Veuillez modifier la variable GTA_PATH dans ce script
    echo.
    echo Autres emplacements courants:
    echo - Steam: C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V
    echo - Epic: C:\Program Files\Epic Games\GTAV
    echo - Rockstar: C:\Program Files\Rockstar Games\Grand Theft Auto V
    pause
    exit /b 1
)

echo VERIFICATION PREREQUIS Enhanced Edition:
echo ----------------------------------------

REM Vérification Script Hook V
if not exist "%GTA_PATH%\ScriptHookV.dll" (
    echo [!] ATTENTION: Script Hook V manquant
    echo     Téléchargez-le depuis: http://dev-c.com/gtav/scripthookv/
    echo.
)

REM Vérification ScriptHookVDotNet
if not exist "%GTA_PATH%\ScriptHookVDotNet.asi" (
    echo [!] ATTENTION: ScriptHookVDotNet manquant  
    echo     Téléchargez nightly build depuis GitHub
    echo.
)

REM Vérification BattlEye
if exist "%GTA_PATH%\BattlEye" (
    echo [!] ATTENTION: BattlEye détecté - Désactivez-le dans Rockstar Launcher
    echo.
)

echo Installation vers: %GTA_PATH%
echo.

REM Création du dossier scripts s'il n'existe pas
if not exist "%GTA_PATH%\scripts" (
    mkdir "%GTA_PATH%\scripts"
    echo Dossier scripts créé.
)

echo Copie des fichiers REALIS...

REM Copie des DLL principales
copy "bin\net48\REALIS.Loader.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Common.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Police.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Events.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Missions.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Traffic.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Services.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Dealership.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\REALIS.Job.dll" "%GTA_PATH%\scripts\" >nul

REM Copie des dépendances
copy "bin\net48\LemonUI.SHVDN3.dll" "%GTA_PATH%\scripts\" >nul
copy "bin\net48\Newtonsoft.Json.dll" "%GTA_PATH%\scripts\" >nul

REM Copie des fichiers de configuration
if not exist "%GTA_PATH%\scripts\Config" mkdir "%GTA_PATH%\scripts\Config"
xcopy "Config\*" "%GTA_PATH%\scripts\Config\" /Y >nul

echo.
echo =======================================================
echo   Installation terminée avec succès !
echo =======================================================
echo.
echo Fichiers copiés vers: %GTA_PATH%\scripts\
echo.
echo PRÊT À TESTER :
echo 1. Lancez GTA V
echo 2. Le mod devrait se charger automatiquement
echo 3. En cas de problème, appuyez sur F4 (console SHVDN)
echo.
pause 