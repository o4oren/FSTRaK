@echo off
echo Building FSTrAk InGame Panel SPB...

REM The MSFS SDK sets the MSFS_SDK environment variable automatically.
REM If it is not set, update the path below to match your SDK installation.
if "%MSFS_SDK%"=="" set MSFS_SDK=C:\MSFS SDK

set TOOL="%MSFS_SDK%\Tools\bin\fspackagetool.exe"

if not exist %TOOL% (
    echo ERROR: fspackagetool.exe not found at %MSFS_SDK%\Tools\bin\
    echo Please install the MSFS SDK or update the MSFS_SDK path in this file.
    pause
    exit /b 1
)

%TOOL% "fstrak-ingame-panel\Build\fstrak-ingame-panel.xml" -nomirroring

if %errorlevel% neq 0 (
    echo ERROR: Build failed. See output above.
    pause
    exit /b 1
)

echo Copying SPB to InGamePanels...
copy /Y "fstrak-ingame-panel\Build\Packages\fstrak-ingame-panel\Build\fstrak-ingame-panel.spb" "fstrak-ingame-panel\InGamePanels\"

if %errorlevel% neq 0 (
    echo ERROR: Copy failed. Check the Build\Packages output directory.
    pause
    exit /b 1
)

echo.
echo Build complete. fstrak-ingame-panel.spb is ready.
echo Copy the fstrak-ingame-panel\ folder to your MSFS Community folder.
pause
