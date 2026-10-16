@echo off
REM Bibliotheca LMS - Windows launcher for setup.ps1
REM Runs the PowerShell setup script without changing the machine's execution policy.
REM Usage examples:
REM   setup.cmd -RepoUrl https://github.com/<team>/<repo>.git
REM   setup.cmd -Branch location/palmerston -Location "Palmerston Library" -ResetDatabase
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" %*
