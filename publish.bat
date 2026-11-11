@echo off
setlocal
pushd "%~dp0"

set "PUBLISH_DIR=%~dp0release\WallpaperLite-win-x64"
set "ARCHIVE=%~dp0release\WallpaperLite-win-x64.zip"

echo Gerando a interface...
pushd "%~dp0frontend"
call npm ci
if errorlevel 1 goto :err_frontend
call npm run build
if errorlevel 1 goto :err_frontend
popd

echo Publicando o aplicativo autocontido para Windows x64...
dotnet publish "%~dp0backend\WallpaperLite.Host\WallpaperLite.Host.csproj" -c Release -r win-x64 --self-contained true -p:UseAppHost=true -o "%PUBLISH_DIR%"
if errorlevel 1 goto :err_publish

copy /Y "%~dp0distribution\LEIA-ME.txt" "%PUBLISH_DIR%\LEIA-ME.txt" >nul
if errorlevel 1 goto :err_publish
copy /Y "%~dp0distribution\Iniciar WallpaperLite.bat" "%PUBLISH_DIR%\Iniciar WallpaperLite.bat" >nul
if errorlevel 1 goto :err_publish

if exist "%ARCHIVE%" del /Q "%ARCHIVE%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%PUBLISH_DIR%' -DestinationPath '%ARCHIVE%' -Force"
if errorlevel 1 goto :err_publish

echo.
echo Pacote pronto:
echo %ARCHIVE%
popd
exit /b 0

:err_frontend
popd
echo Falha ao gerar a interface. Verifique Node.js/npm e tente novamente.
popd
pause
exit /b 1

:err_publish
echo Falha ao publicar ou compactar o aplicativo. Verifique o .NET SDK 8 e tente novamente.
popd
pause
exit /b 1
