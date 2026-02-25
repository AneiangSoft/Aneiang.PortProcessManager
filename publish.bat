@echo off
setlocal

set PROJECT_PATH=PortProcessManager\PortProcessManager.csproj
set OUTPUT_BASE=publish_output

echo [1/2] Publishing for win-x64 (Self-Contained)...
dotnet publish %PROJECT_PATH% -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o %OUTPUT_BASE%\win-x64

echo [2/2] Publishing for win-x86 (Self-Contained)...
dotnet publish %PROJECT_PATH% -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -o %OUTPUT_BASE%\win-x86

echo.
echo Done! Files are in %OUTPUT_BASE%
pause
