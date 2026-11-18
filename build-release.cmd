@echo off
rem Builds a single self-contained SvitloTray.exe into .\publish
cd /d "%~dp0"
dotnet publish SvitloTray.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish > publish-log.txt 2>&1
echo %ERRORLEVEL% > publish-exit.txt
