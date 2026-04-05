@echo off
echo Building FSTrAk InGame Panel SPB...

if "%MSFS_SDK%"=="" set MSFS_SDK=C:\MSFS SDK
if "%MSFS_SDK:~-1%"=="\" set MSFS_SDK=%MSFS_SDK:~0,-1%

set TOOL=%MSFS_SDK%\Tools\bin\fspackagetool.exe
set BAT_DIR=%~dp0
set BAT_DIR=%BAT_DIR:~0,-1%

if not exist "%TOOL%" (
    echo ERROR: fspackagetool.exe not found at: %TOOL%
    pause
    exit /b 1
)

echo Tool: %TOOL%
echo.

pushd "%BAT_DIR%\fstrak-ingame-panel"
"%TOOL%" package.xml 2>&1
set BUILD_RESULT=%errorlevel%
popd

echo.
echo fspackagetool exit code: %BUILD_RESULT%
echo.

if %BUILD_RESULT% neq 0 (
    echo ERROR: Build failed.
    echo Listing output directory:
    dir "%BAT_DIR%\fstrak-ingame-panel" /s /b 2>&1
    pause
    exit /b 1
)

echo Copying SPB...
copy /Y "%BAT_DIR%\fstrak-ingame-panel\Packages\fstrak-ingame-panel\InGamePanels\fstrak-ingame-panel.spb" "%BAT_DIR%\fstrak-ingame-panel\InGamePanels\"

echo.
echo Build complete! Now run: python generate_layout.py
echo Then copy fstrak-ingame-panel\ to your MSFS Community folder.
pause
