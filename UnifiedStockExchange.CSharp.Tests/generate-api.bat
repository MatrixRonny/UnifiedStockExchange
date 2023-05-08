@echo off

if exist ..\UnifiedStockExchangeSdk.CSharp rmdir /S /Q ..\UnifiedStockExchangeSdk.CSharp

openapi-generator generate -i http://localhost:5097/swagger/v1/swagger.json -g csharp-netcore --additional-properties packageName=UnifiedStockExchangeSdk.CSharp -o ..\UnifiedStockExchangeSdk.CSharp