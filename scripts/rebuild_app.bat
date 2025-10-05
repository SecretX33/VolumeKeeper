@echo off
SETLOCAL EnableExtensions

cls

echo ------------------------------------
echo Building VolumeKeeper application...
echo ------------------------------------
echo[

:: Fetch the build mode from the first parameter (if provided)
set "MODE=%1"

if "%MODE%"=="" (
    set "MODE=Debug"
)
if /i "%MODE%"=="debug" (
    set "MODE=Debug"
)
if /i "%MODE%"=="release" (
    set "MODE=Release"
)

:: Validate the 'mode' input
if /i not "%MODE%"=="debug" if /i not "%MODE%"=="release" (
    echo Invalid mode specified: '%MODE%'. Use 'Debug' or 'Release'.
    goto :END
)

for /d %%i in (VolumeKeeper\bin\%MODE%\net9.0-*) do (
    echo Deleting folder %%i
    rmdir /s /q "%%i"
)

echo Building in '%MODE%' mode...

if "%MODE%"=="Debug" (
    dotnet publish --configuration Debug --runtime win-x64 -p:DebugType=false -p:DebugSymbols=false VolumeKeeper\VolumeKeeper.csproj
) else (
    dotnet publish --configuration Release --runtime win-x64 --self-contained true -p:PublishReadyToRun=true VolumeKeeper\VolumeKeeper.csproj
)

:END
endlocal
