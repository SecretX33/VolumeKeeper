@echo off
SETLOCAL EnableExtensions

for /d %%i in (VolumeKeeper\bin\Release\net9.0-*) do (
    echo Deleting folder %%i
    rmdir /s /q "%%i"
)
dotnet publish --configuration Release --runtime win-x64 --self-contained true -p:PublishReadyToRun=true VolumeKeeper\VolumeKeeper.csproj

endlocal
