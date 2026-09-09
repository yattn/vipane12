@echo off
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "TESTDIR=%~dp0tests\run-%RANDOM%-%RANDOM%"
if exist "%TESTDIR%" exit /b 1
mkdir "%TESTDIR%"
if errorlevel 1 exit /b 1
"%CSC%" /nologo /warn:4 /warnaserror /t:exe /main:RegressionTests /out:"%TESTDIR%\RegressionTests.exe" "%~dp0tests\RegressionTests.cs" "%~dp0src\*.cs" /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll
if errorlevel 1 exit /b 1
"%TESTDIR%\RegressionTests.exe"
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" exit /b %RESULT%
for /l %%i in (1,1,20) do (
  if not exist "%TESTDIR%" goto :clean_done
  rmdir /s /q "%TESTDIR%" >nul 2>&1
  if not exist "%TESTDIR%" goto :clean_done
  ping -n 2 127.0.0.1 >nul
)
:clean_done
exit /b %RESULT%
