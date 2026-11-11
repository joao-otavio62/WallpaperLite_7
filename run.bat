@echo off
rem Gera o frontend e abre o programa (modo Release: não precisa do servidor do Vite).
cd /d "%~dp0frontend"
call npm install || goto :err
call npm run build || goto :err
cd /d "%~dp0"
dotnet run -c Release --project backend\WallpaperLite.Host
goto :eof
:err
echo Falha ao gerar o frontend (Node.js instalado?).
pause
