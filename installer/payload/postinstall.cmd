@echo off
setlocal
set "DST_DIR=%LOCALAPPDATA%\DSTServer"
if not exist "%DST_DIR%" mkdir "%DST_DIR%"
xcopy /E /I /Y "%~dp0*" "%DST_DIR%\" >nul
del /q "%DST_DIR%\postinstall.cmd" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws=New-Object -ComObject WScript.Shell; $desk=$ws.SpecialFolders('Desktop'); $s=$ws.CreateShortcut($desk+'\DSTServer.lnk'); $s.TargetPath='%DST_DIR%\DSTserver.exe'; $s.WorkingDirectory='%DST_DIR%'; $s.IconLocation='%DST_DIR%\DSTserver.exe,0'; $s.Save(); $prog=$ws.SpecialFolders('Programs'); $s2=$ws.CreateShortcut($prog+'\DSTServer.lnk'); $s2.TargetPath='%DST_DIR%\DSTserver.exe'; $s2.WorkingDirectory='%DST_DIR%'; $s2.Save()"
start "" "%DST_DIR%\DSTserver.exe"
endlocal
