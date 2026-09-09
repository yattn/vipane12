@echo off
setlocal
set CSC=
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not defined CSC if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not defined CSC for /f "delims=" %%i in ('where csc.exe 2^>nul') do if not defined CSC set "CSC=%%i"
if not defined CSC (
  echo csc.exe not found.
  exit /b 1
)
set "ICONICO=%TEMP%\ViPane12-icon.ico"
set "ICONBUILDER=%TEMP%\ViPane12-IconBuilder-%RANDOM%.exe"
"%CSC%" /nologo /warn:4 /warnaserror /t:exe /out:"%ICONBUILDER%" "%~dp0tools\IconBuilder.cs" /r:System.dll /r:System.Drawing.dll
if errorlevel 1 goto :icon_error
"%ICONBUILDER%" "%~dp0icon.jpg" "%ICONICO%"
if errorlevel 1 goto :icon_error
del /q "%ICONBUILDER%" >nul 2>&1
"%CSC%" /nologo /warn:4 /warnaserror /t:winexe /win32icon:"%ICONICO%" /out:"%~dp0ViPane12.exe" "%~dp0src\Program.cs" "%~dp0src\MainForm.cs" "%~dp0src\FilePane.cs" "%~dp0src\FileOperations.cs" "%~dp0src\CommandRunner.cs" "%~dp0src\Workspace.cs" "%~dp0src\SelectionList.cs" "%~dp0src\FuzzyJump.cs" /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll
if errorlevel 1 exit /b 1
echo build ok: ViPane12.exe
exit /b 0

:icon_error
del /q "%ICONBUILDER%" >nul 2>&1
exit /b 1
