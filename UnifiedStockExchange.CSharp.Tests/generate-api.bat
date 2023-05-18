@echo off

cd /D "%~dp0"

if exist ..\UnifiedStockExchange.Sdk.CSharp rmdir /S /Q ..\UnifiedStockExchange.Sdk.CSharp

openapi-generator generate -i http://localhost:5097/swagger/v1/swagger.json -g csharp-netcore --additional-properties packageName=UnifiedStockExchange.Sdk.CSharp -o ..\UnifiedStockExchange.Sdk.CSharp