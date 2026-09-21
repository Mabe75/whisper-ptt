@echo off
cd /d "%~dp0"
set "APPDIR=%~dp0"
if "%APPDIR:~-1%"=="\" set "APPDIR=%APPDIR:~0,-1%"
set "VBS=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\voice-ptt.vbs"
set "REGKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\VoicePTT"
echo Voice Push-to-Talk wird deinstalliert...
powershell -NoProfile -Command "Get-Process pythonw -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*VoicePTT*' } | Stop-Process -Force" >nul 2>&1
if exist "%VBS%" del "%VBS%"
reg delete "%REGKEY%" /f >nul 2>&1
if exist "%APPDIR%\venv" rmdir /s /q "%APPDIR%\venv"
echo Fertig: Autostart entfernt, venv geloescht, Registrierung entfernt.
echo Ordner: %APPDIR%
choice /M "Den kompletten Ordner ebenfalls loeschen"
if errorlevel 2 goto done
start "" cmd /c "cd /d %TEMP% & timeout /t 2 >nul & rmdir /s /q ""%APPDIR%"""
exit /b 0
:done
pause
