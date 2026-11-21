@echo off
setlocal
chcp 65001 >nul
set "EXHIBIT_LOG=%LOCALAPPDATA%\ReversalTowerExhibit"
if not exist "%EXHIBIT_LOG%" mkdir "%EXHIBIT_LOG%"
echo 역전의 탑 테스트 빌드를 실행합니다. 로그: "%EXHIBIT_LOG%\launcher.log"
if not exist "%~dp0runtime\python\python.exe" goto gameonly
if not exist "%~dp0presentation.py" goto gameonly
"%~dp0runtime\python\python.exe" -X utf8 -u "%~dp0presentation.py" >> "%EXHIBIT_LOG%\launcher.log" 2>&1
if errorlevel 1 goto gameonly
exit /b 0
:gameonly
echo 통합 실행기 오류. 게임 단독 실행을 시도합니다. >> "%EXHIBIT_LOG%\launcher.log"
if exist "%~dp0game\ReversalTower.exe" (
 start "" /D "%~dp0game" "%~dp0game\ReversalTower.exe"
 exit /b 0
)
echo 게임 또는 런타임 파일이 없습니다. ZIP 전체를 다시 압축 해제하세요.
pause
exit /b 1
