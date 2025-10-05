@echo off
SETLOCAL EnableExtensions

cls

echo ------------------------------------
echo Building VolumeKeeper installer...
echo ------------------------------------
echo[

iscc .\scripts\inno_dev.iss

endlocal
