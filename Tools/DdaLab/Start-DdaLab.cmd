@echo off
setlocal
chcp 65001 >nul
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"
if not exist "%~dp0launch.py" (
  echo 필수 파일 없음: "%~dp0launch.py". 저장소 전체를 내려받으세요.
  pause
  exit /b 1
)
py -3 -c "import sys;sys.exit(0 if sys.version_info >= (3,10) else 1)" >nul 2>&1
if not errorlevel 1 (
  py -3 -X utf8 "%~dp0launch.py" %*
  goto finished
)
python -c "import sys;sys.exit(0 if sys.version_info >= (3,10) else 1)" >nul 2>&1
if not errorlevel 1 (
  python -X utf8 "%~dp0launch.py" %*
  goto finished
)
echo Python 3.10 이상을 찾지 못했습니다. Python 설치 후 이 창을 다시 여세요.
echo Git과 PowerShell은 이 실행기에 필요하지 않습니다.
pause
exit /b 1
:finished
set "LAB_EXIT=%ERRORLEVEL%"
if not "%LAB_EXIT%"=="0" (
  echo DDA Lab 실행 실패. 위 오류와 %%LOCALAPPDATA%%\ReversalTower-DdaLab\launcher.log를 확인하세요.
  pause
)
exit /b %LAB_EXIT%
