using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.Utility;

namespace UnifiedStockExchange.Services
{
    public class PriceExchangeService
    {
        Dictionary<string, List<string>> _priceQuotes = new Dictionary<string, List<string>>();
        Dictionary<string, PriceUpdate> _priceHandlers = new Dictionary<string, PriceUpdate>();
        Dictionary<PriceUpdate, List<PriceUpdate>> _priceListeners = new Dictionary<PriceUpdate, List<PriceUpdate>>();

        public PriceUpdate ListenForUpdates(string exchangeName, ValueTuple<string, string> tradingPair)
        {
            lock (_priceQuotes)
            lock (_priceHandlers)
            lock (_priceListeners)
            {
                string quote = $"{tradingPair.Item1}-{tradingPair.Item2}";
                if (_priceQuotes.ContainsKey(exchangeName) && _priceQuotes[exchangeName].Contains(quote))
                    throw new ApplicationException("A listener has already been registered for that exchange and quote.");

                string exchangeQuote = $"{exchangeName}|{quote}";
                switch (exchangeName)
                {
                    case "CoinMarketCap":
                    {
                        ValueReference<PriceUpdate> valRef = new();
                        PriceUpdate del = (time, price, amount) =>
                        {
                            List<PriceUpdate> forward = _priceListeners[valRef.Value!];
                            lock(forward)
                            {
                                Parallel.ForEach(forward, update =>
                                {
                                    try
                                    {
                                        update(time, price, amount);
                                    }
                                    catch
                                    {
                                    }
                                });
                            }
                        };
                        valRef.Value = del;

                        if (!_priceQuotes.ContainsKey(exchangeName))
                        {
                            _priceQuotes[exchangeName] = new();
                        }
                        _priceQuotes[exchangeName].Add(quote);
                        _priceHandlers[exchangeQuote] = del;
                        _priceListeners[del] = new List<PriceUpdate>();

                        return del;
                    }

                    default:
                        throw new ApplicationException("Specified exchange does not exist.");
                }
            }
        }

        public void CancelListen(string exchangeName, ValueTuple<string, string> tradingPair)
        {
            lock (_priceQuotes)
            lock (_priceHandlers)
            lock (_priceListeners)
            {
                string quote = $"{tradingPair.Item1}-{tradingPair.Item2}";
                string exchangeQuote = $"{exchangeName}|{quote}";
             
                PriceUpdate del = _priceHandlers[exchangeQuote];
                _priceListeners.Remove(del);
                _priceHandlers.Remove(exchangeQuote);

                _priceQuotes[exchangeName].Remove(quote);
                if (_priceQuotes[exchangeName].Count == 0)
                {
                    _priceQuotes.Remove(exchangeName);
                }
            }
        }

        public void RegisterListener(string exchangeName, ValueTuple<string, string> tradingPair, PriceUpdate listener)
        {
            lock (_priceHandlers)
            lock (_priceListeners)
            {
                string exchangeQuote = $"{exchangeName}|{tradingPair.Item1}-{tradingPair.Item2}";
                if (!_priceHandlers.ContainsKey(exchangeQuote))
                    throw new ApplicationException("Specified exchange and quote does not exist.");

                PriceUpdate del = _priceHandlers[exchangeQuote];
                _priceListeners[del].Add(listener);
            }
        }

        public void RemoveListener(string exchangeName, ValueTuple<string, string> tradingPair, PriceUpdate listener)
        {
            lock (_priceHandlers)
            lock (_priceListeners)
            {
                string exchangeQuote = $"{exchangeName}|{tradingPair.Item1}-{tradingPair.Item2}";
                if (!_priceHandlers.ContainsKey(exchangeQuote))
                    throw new ApplicationException("Specified exchange and quote does not exist.");

                PriceUpdate del = _priceHandlers[exchangeQuote];
                _priceListeners[del].Remove(listener);
            }
        }
    }
}
