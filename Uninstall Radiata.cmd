@echo off
rem Radiata uninstaller. Launches the app's uninstall flow, which removes Radiata and its Windows
rem changes (startup entry, controller cloak/allow-list, sign-in recovery task, this folder). It also
rem OFFERS to uninstall the shared ViGEmBus and HidHide drivers, but leaves them installed by default.
start "" "%~dp0Radiata.exe" --uninstall
