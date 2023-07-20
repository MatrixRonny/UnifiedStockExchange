REM @echo off

SET ServerPath=UnifiedStockExchange\bin\Debug\net6.0\UnifiedStockExchange.exe

if not exist %ServerPath% (
	echo Please build UnifiedStockExchange project first.
	exit 1
)

SET ASPNETCORE_ENVIRONMENT=Development
SET AspNetUrls=https://localhost:7288;http://localhost:5098
SET StartServerCmd=powershell.exe -Command "(Start-Process -FilePath %ServerPath% -Argument \"--urls %AspNetUrls%\" -PassThru -WindowStyle Hidden).Id"
%StartServerCmd% >temp & (set /p PID=)<temp & del temp

cd /D "%~dp0"

if exist UnifiedStockExchange.Sdk.CSharp rmdir /S /Q UnifiedStockExchange.Sdk.CSharp

call openapi-generator generate -i http://localhost:5098/swagger/v1/swagger.json -g csharp-netcore --additional-properties packageName=UnifiedStockExchange.Sdk.CSharp -o UnifiedStockExchange.Sdk.CSharp

taskkill /PID %PID%