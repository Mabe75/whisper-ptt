@echo off
setlocal EnableExtensions
title Voice Push-to-Talk Setup
set "SELF=%~f0"
set "TARGET=%LOCALAPPDATA%\VoicePTT"
set "VBS=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\voice-ptt.vbs"
set "REGKEY=HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\VoicePTT"

:menu
cls
echo ==================================================
echo    Voice Push-to-Talk  -  Setup (eine Datei)
echo    Ziel: %TARGET%
call :status
echo ==================================================
echo.
echo   [1] Installieren / Aktualisieren
echo   [2] Autostart aktivieren
echo   [3] Autostart deaktivieren
echo   [4] Client starten
echo   [5] Deinstallieren
echo   [0] Beenden
echo.
set /p "CH=Auswahl: "
if "%CH%"=="1" goto install
if "%CH%"=="2" goto aon
if "%CH%"=="3" goto aoff
if "%CH%"=="4" goto startc
if "%CH%"=="5" goto uninstall
if "%CH%"=="0" exit /b 0
goto menu

:status
if exist "%TARGET%\venv\Scripts\pythonw.exe" (echo    Status: installiert) else (echo    Status: nicht installiert)
if exist "%VBS%" (echo    Autostart: AN) else (echo    Autostart: aus)
exit /b

:install
echo Entpacke eingebettete Programmdateien...
if not exist "%TARGET%" mkdir "%TARGET%"
for /f "delims=:" %%a in ('findstr /n /b /c:":::PAYLOAD:::" "%SELF%"') do set "MK=%%a"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$n=[int]$env:MK; $b=((Get-Content -LiteralPath $env:SELF) | Select-Object -Skip $n) -join ''; [IO.File]::WriteAllBytes($env:TEMP+'\vptt.zip',[Convert]::FromBase64String($b)); Expand-Archive -Force ($env:TEMP+'\vptt.zip') $env:TARGET" || (echo Entpacken fehlgeschlagen. & pause & goto menu)
del "%TEMP%\vptt.zip" >nul 2>&1
call :findpy
if not defined PY (
  echo.
  echo Python nicht gefunden - versuche automatische Installation via winget...
  winget install -e --id Python.Python.3.11 --scope user --accept-source-agreements --accept-package-agreements
  call :findpy
)
if not defined PY (
  echo.
  echo Python konnte nicht automatisch installiert werden.
  echo Bitte von https://www.python.org/downloads/windows/ installieren
  echo ("Add python.exe to PATH" anhaken) und Setup erneut starten.
  pause & goto menu
)
echo Erstelle venv mit %PY% ...
%PY% -m venv "%TARGET%\venv"
if not exist "%TARGET%\venv\Scripts\python.exe" ( echo venv fehlgeschlagen. & pause & goto menu )
echo Lade und installiere Python-Pakete von PyPI...
"%TARGET%\venv\Scripts\python.exe" -m pip install --upgrade pip
"%TARGET%\venv\Scripts\python.exe" -m pip install -r "%TARGET%\requirements.txt"
call :reg_add
echo.
echo Installation abgeschlossen.
choice /M "Autostart aktivieren"
if not errorlevel 2 call :write_vbs
choice /M "Client jetzt starten"
if not errorlevel 2 call :do_start
goto menu

:findpy
set "PY="
where py >nul 2>&1 && set "PY=py"
if not defined PY ( python -c "import sys" >nul 2>&1 && set "PY=python" )
if not defined PY ( for /f "delims=" %%p in ('dir /b /s "%LOCALAPPDATA%\Programs\Python\python.exe" 2^>nul') do set "PY=%%p" )
exit /b

:reg_add
reg add "%REGKEY%" /v DisplayName /d "Voice Push-to-Talk" /f >nul
reg add "%REGKEY%" /v DisplayVersion /d "1.0" /f >nul
reg add "%REGKEY%" /v Publisher /d "lokal" /f >nul
reg add "%REGKEY%" /v InstallLocation /d "%TARGET%" /f >nul
reg add "%REGKEY%" /v DisplayIcon /d "%SystemRoot%\System32\shell32.dll,138" /f >nul
reg add "%REGKEY%" /v UninstallString /d "\"%TARGET%\uninstall.bat\"" /f >nul
reg add "%REGKEY%" /v NoModify /t REG_DWORD /d 1 /f >nul
reg add "%REGKEY%" /v NoRepair /t REG_DWORD /d 1 /f >nul
exit /b

:write_vbs
> "%VBS%" echo Set s = CreateObject^("WScript.Shell"^)
>> "%VBS%" echo s.Run """%TARGET%\venv\Scripts\pythonw.exe"" ""%TARGET%\client.py""", 0, False
exit /b

:aon
if not exist "%TARGET%\venv\Scripts\pythonw.exe" ( echo Erst installieren. & pause & goto menu )
call :write_vbs
echo Autostart aktiviert. & pause & goto menu

:aoff
if exist "%VBS%" del "%VBS%"
echo Autostart deaktiviert. & pause & goto menu

:startc
call :do_start
goto menu

:do_start
if not exist "%TARGET%\venv\Scripts\pythonw.exe" ( echo Erst installieren. & pause & exit /b )
start "" "%TARGET%\venv\Scripts\pythonw.exe" "%TARGET%\client.py"
exit /b

:uninstall
if exist "%TARGET%\uninstall.bat" ( call "%TARGET%\uninstall.bat" ) else ( echo Nichts installiert. & pause )
goto menu
:::PAYLOAD:::
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
