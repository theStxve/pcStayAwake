@echo off
setlocal
rem ============================================================
rem  pcStayAwake - Build
rem  Nutzt den C#-Compiler, der mit Windows (.NET Framework 4.x)
rem  mitgeliefert wird. Es muss nichts installiert werden.
rem
rem  Ergebnis:  dist\pcStayAwake.exe        (die App)
rem             dist\pcStayAwake-Setup.exe  (Installer, enthaelt die App)
rem ============================================================
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [FEHLER] C#-Compiler ^(.NET Framework 4.x^) nicht gefunden.
    exit /b 1
)

set "FLAGS=/nologo /optimize+ /utf8output /codepage:65001 /r:System.Windows.Forms.dll /r:System.Drawing.dll"
set "SHARED=src\Shared\*.cs"

if not exist obj mkdir obj
if not exist dist mkdir dist

echo [1/3] Icons erzeugen...
"%CSC%" %FLAGS% /target:exe /out:obj\IconGen.exe tools\IconGen.cs %SHARED% || goto :error
obj\IconGen.exe assets || goto :error

echo [2/3] pcStayAwake.exe bauen...
"%CSC%" %FLAGS% /target:winexe /win32icon:assets\app.ico /win32manifest:assets\app.manifest ^
    /out:dist\pcStayAwake.exe src\pcStayAwake\*.cs %SHARED% || goto :error

echo [3/3] pcStayAwake-Setup.exe bauen...
"%CSC%" %FLAGS% /target:winexe /win32icon:assets\app.ico /win32manifest:assets\app.manifest ^
    /resource:dist\pcStayAwake.exe,pcStayAwake.exe.payload ^
    /out:dist\pcStayAwake-Setup.exe src\Setup\*.cs %SHARED% || goto :error

echo.
echo Fertig:
echo   dist\pcStayAwake.exe
echo   dist\pcStayAwake-Setup.exe
exit /b 0

:error
echo.
echo [FEHLER] Build fehlgeschlagen.
exit /b 1
