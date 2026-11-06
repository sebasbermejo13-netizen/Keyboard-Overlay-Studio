@echo off
rem Vuelve a crear "Keyboard Overlay Studio.exe" con el compilador que trae Windows (.NET Framework 4.8).
rem Solo hace falta si cambias el codigo o si Windows bloquea el .exe incluido.
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:winexe /unsafe /optimize+ /win32icon:icon.ico /out:"..\Keyboard Overlay Studio.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:Microsoft.CSharp.dll /r:System.Core.dll /r:Accessibility.dll *.cs
if errorlevel 1 (echo. & echo Hubo un error al compilar.) else (echo. & echo Listo: Keyboard Overlay Studio.exe creado.)
pause
