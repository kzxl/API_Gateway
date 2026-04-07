@echo off
echo Starting Admin UI Web Server...
echo.
echo Admin UI will be available at: http://localhost:8888
echo.
echo Press Ctrl+C to stop the server
echo.

cd admin-ui
python -m http.server 8888
