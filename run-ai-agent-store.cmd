@echo off
setlocal
pushd "%~dp0"
if errorlevel 1 exit /b 1
dotnet run --project SoftwareFactory -- --scenario ai-agent-store %*
set "RUN_EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %RUN_EXIT_CODE%
