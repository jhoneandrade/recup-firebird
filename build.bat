@echo off
setlocal
echo ========================================================
echo   Compilando Recuperador de Banco Firebird (RecupBD)
echo ========================================================

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
)
if not exist "%CSC%" (
    echo [ERRO] Compilador csc.exe nao encontrado no .NET Framework 4.0.
    pause
    exit /b 1
)

echo Usando compilador: %CSC%
"%CSC%" /nologo /codepage:65001 /target:winexe /win32icon:app_icon.ico /win32manifest:app.manifest /r:System.ServiceProcess.dll /out:RecupBD.exe RecupBD.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCESSO] RecupBD.exe gerado com sucesso!
) else (
    echo.
    echo [ERRO] Falha na compilacao. Verifique as mensagens acima.
)

pause
