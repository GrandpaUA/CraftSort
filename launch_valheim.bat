@echo off
REM Launch Valheim with BepInEx via Doorstop — replicates r2modman "Craft" profile launch
REM r2modman modifies doorstop_config.ini in the game dir, then launches through Steam

set "VALHEIM_DIR=C:\Program Files (x86)\Steam\steamapps\common\Valheim"
set "PROFILE_DIR=%APPDATA%\r2modmanPlus-local\Valheim\profiles\Craft"
set "PRELOADER=%PROFILE_DIR%\BepInEx\core\BepInEx.Preloader.dll"

REM Point doorstop at the profile's BepInEx (absolute path)
powershell -Command "(Get-Content '%VALHEIM_DIR%\doorstop_config.ini') -replace '^target_assembly=.*', 'target_assembly=%PRELOADER%' | Set-Content '%VALHEIM_DIR%\doorstop_config.ini'"

echo Starting Valheim via Steam with BepInEx (Craft profile)...
echo   target_assembly=%PRELOADER%
start steam://rungameid/892970
