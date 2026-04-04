@echo off
echo Building FSTrAk InGame Panel SPB...

REM Get the directory of this batch file (no trailing backslash)
set BAT_DIR=%~dp0
set BAT_DIR=%BAT_DIR:~0,-1%

REM fspackagetool.exe path — MSFS SDK sets MSFS_SDK env var automatically.
REM Strip any trailing backslash from MSFS_SDK before appending our path.
if "%MSFS_SDK%"=="" set MSFS_SDK=C:\MSFS SDK
if "%MSFS_SDK:~-1%"=="\" set MSFS_SDK=%MSFS_SDK:~0,-1%

set TOOL=%MSFS_SDK%\Tools\bin\fspackagetool.exe

if not exist "%TOOL%" (
    echo ERROR: fspackagetool.exe not found at: %TOOL%
    echo Please install the MSFS SDK or update the MSFS_SDK path in this file.
    pause
    exit /b 1
)

set PROJECT=%BAT_DIR%\fstrak-ingame-panel\Build\fstrak-ingame-panel.xml

if not exist "%PROJECT%" (
    echo ERROR: Project file not found at: %PROJECT%
    pause
    exit /b 1
)

echo Tool:    %TOOL%
echo Project: Build\fstrak-ingame-panel.xml
echo WorkDir: %BAT_DIR%\fstrak-ingame-panel
echo.

REM Run from fstrak-ingame-panel\ so PackageSources\ resolves correctly
pushd "%BAT_DIR%\fstrak-ingame-panel"
"%TOOL%" "Build\fstrak-ingame-panel.xml" 2>&1
set BUILD_RESULT=%errorlevel%
popd
echo.
echo fspackagetool exit code: %BUILD_RESULT%
echo.

REM Show whatever the tool produced
echo Listing fstrak-ingame-panel directory after tool run:
dir "%BAT_DIR%\fstrak-ingame-panel\" /s /b 2>&1
echo.

if %BUILD_RESULT% neq 0 (
    echo ERROR: Build failed with exit code %BUILD_RESULT%.
    pause
    exit /b 1
)

set SPB_SRC=%BAT_DIR%\fstrak-ingame-panel\Build\Packages\fstrak-ingame-panel\fstrak-ingame-panel.spb
set SPB_DST=%BAT_DIR%\fstrak-ingame-panel\InGamePanels\

echo Copying SPB...
echo From: %SPB_SRC%
echo To:   %SPB_DST%

copy /Y "%SPB_SRC%" "%SPB_DST%"

if %errorlevel% neq 0 (
    echo ERROR: Copy failed.
    echo Expected SPB at: %SPB_SRC%
    echo Listing Build\Packages output:
    dir "%BAT_DIR%\fstrak-ingame-panel\Build\Packages\" /s /b 2>&1
    pause
    exit /b 1
)

echo.
echo Build complete. fstrak-ingame-panel.spb is ready.
echo Copy the fstrak-ingame-panel\ folder to your MSFS Community folder.
pause
