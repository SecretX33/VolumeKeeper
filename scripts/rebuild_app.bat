@echo off
SETLOCAL EnableExtensions

rmdir /s /q VolumeKeeper\bin\Release\net9.0-windows10.0.19041.0
dotnet publish --configuration Release --runtime win-x64 --self-contained true -p:PublishReadyToRun=true VolumeKeeper\VolumeKeeper.csproj

endlocal
