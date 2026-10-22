@echo off
rem Build with the in-box .NET Framework compiler. Zero toolchain deps.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /win32icon:src\app.ico /win32manifest:src\app.manifest ^
  /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll ^
  /out:DeskPets.exe src\*.cs

if %errorlevel% neq 0 (echo BUILD FAILED & exit /b 1)
copy /Y src\DeskPets.exe.config DeskPets.exe.config >nul
if %errorlevel% neq 0 (echo CONFIG COPY FAILED & exit /b 1)
echo BUILD OK: DeskPets.exe
