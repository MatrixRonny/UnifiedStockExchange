# UnifiedStockExchange
Create a standard API to access core features accross any stock exchange.

## Milestones
- Create Listen/Forward archtiecture for price changes.
- Implement UnifiedStockExchange.Provider.CoinMarketCap-CSharp
- Implement UnifiedStockExchange.Listener.CSharp
- Test entire flow with price provider, UnifiedStockExchange and listener.
- Record price changes for several trading pairs to DB.
- Extend architecture with capability of placing stock orders.
- Implement UnifiedStockExchange.StockExchange.Binance (C# only)

## First time run
- Enable Git submodules with `git submodules init`
- Clone and pull submodules recursively with `git pull --recurse-submodules`
- Use `generate-api.bat` script to create REST client SDK from Swagger.