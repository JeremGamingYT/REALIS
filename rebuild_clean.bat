@echo off
echo =======================================================
echo         REALIS - Nettoyage et Rebuild
echo =======================================================
echo.

echo Nettoyage des fichiers temporaires...
rd /s /q bin 2>nul
rd /s /q obj 2>nul
rd /s /q REALIS.Common\bin 2>nul
rd /s /q REALIS.Common\obj 2>nul
rd /s /q REALIS.Loader\bin 2>nul
rd /s /q REALIS.Loader\obj 2>nul
rd /s /q REALIS.Police\bin 2>nul
rd /s /q REALIS.Police\obj 2>nul
rd /s /q REALIS.Events\bin 2>nul
rd /s /q REALIS.Events\obj 2>nul
rd /s /q REALIS.Traffic\bin 2>nul
rd /s /q REALIS.Traffic\obj 2>nul
rd /s /q REALIS.Services\bin 2>nul
rd /s /q REALIS.Services\obj 2>nul
rd /s /q REALIS.Job\bin 2>nul
rd /s /q REALIS.Job\obj 2>nul
rd /s /q REALIS.Missions\bin 2>nul
rd /s /q REALIS.Missions\obj 2>nul
rd /s /q REALIS.Dealership\bin 2>nul
rd /s /q REALIS.Dealership\obj 2>nul

echo.
echo Nettoyage terminé.
echo.

echo Restoration des packages NuGet...
dotnet restore
if %errorlevel% neq 0 (
    echo ERREUR: Échec de la restauration des packages
    pause
    exit /b 1
)

echo.
echo Compilation du projet...
dotnet build --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo ERREUR: Échec de la compilation
    pause
    exit /b 1
)

echo.
echo =======================================================
echo   Compilation terminée avec succès !
echo =======================================================
echo.
echo Fichiers générés dans le dossier bin/net48/
echo.
echo Vous pouvez maintenant exécuter install_to_gta.bat
echo pour copier les fichiers vers GTA V.
echo.
pause 