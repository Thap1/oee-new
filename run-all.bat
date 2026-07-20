@echo off
REM Starts the API (http profile, http://localhost:5067) and the Angular dev server
REM (http://localhost:4200, proxies /api to the API per proxy.conf.json), each in its own window.

set ROOT=%~dp0

start "OeeNew API" cmd /k "cd /d "%ROOT%src\OeeNew.Api" && dotnet run --launch-profile http"
start "OeeNew Web" cmd /k "cd /d "%ROOT%web\oee-shell" && npm start"

echo Started API and Web dev server in separate windows.
