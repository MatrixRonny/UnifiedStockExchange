using System.Collections.Immutable;
using UnifiedStockExchange.Contracts;
using UnifiedStockExchange.Domain.Utility;
using UnifiedStockExchange.Utility;

namespace UnifiedStockExchange.Services
{
    public class PriceExchangeService
    {
        // [exchangeName][index] = pairName
        Dictionary<string, List<string>> _priceQuotes = new Dictionary<string, List<string>>();

        // [exchangeQuote] = PriceUpdateHandler
        Dictionary<string, PriceUpdateHandler> _priceHandlers = new Dictionary<string, PriceUpdateHandler>();

        // [fromHandler][index] = toHandler
        Dictionary<PriceUpdateHandler, List<PriceUpdateHandler>> _priceListeners = new Dictionary<PriceUpdateHandler, List<PriceUpdateHandler>>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> ActiveExchangeQuotes =>
            _priceQuotes.ToImmutableDictionary(it => it.Key, it => (IReadOnlyList<string>)it.Value);

        public PriceUpdateHandler ListenForUpdates(string exchangeName, ValueTuple<string, string> tradingPair)
        {
            lock (_priceQuotes)
            lock (_priceHandlers)
            lock (_priceListeners)
            {
                if (_priceQuotes.ContainsKey(exchangeName) && _priceQuotes[exchangeName].Contains(tradingPair.ToName()))
                    throw new ApplicationException("A listener has already been registered for that exchange and quote.");

                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                switch (exchangeName)
                {
                    case "CoinMarketCap":
                    {
                        ValueReference<PriceUpdateHandler> valRef = new();
                        PriceUpdateHandler del = (time, price, amount) =>
                        {
                            List<PriceUpdateHandler> forward = _priceListeners[valRef.Value!];
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
                        _priceQuotes[exchangeName].Add(tradingPair.ToName());
                        _priceHandlers[exchangeQuote] = del;
                        _priceListeners[del] = new List<PriceUpdateHandler>();

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
                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
             
                PriceUpdateHandler del = _priceHandlers[exchangeQuote];
                _priceListeners.Remove(del);
                _priceHandlers.Remove(exchangeQuote);

                _priceQuotes[exchangeName].Remove(tradingPair.ToName());
                if (_priceQuotes[exchangeName].Count == 0)
                {
                    _priceQuotes.Remove(exchangeName);
                }
            }
        }
        public void RegisterListener(string exchangeName, ValueTuple<string, string> tradingPair, PriceUpdateHandler listener)
        {
            lock (_priceHandlers)
            lock (_priceListeners)
            {
                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                if (!_priceHandlers.ContainsKey(exchangeQuote))
                    throw new ApplicationException("Specified exchange and quote does not exist.");

                PriceUpdateHandler del = _priceHandlers[exchangeQuote];
                _priceListeners[del].Add(listener);
            }
        }

        public void RemoveListener(string exchangeName, ValueTuple<string, string> tradingPair, PriceUpdateHandler listener)
        {
            lock (_priceHandlers)
            lock (_priceListeners)
            {
                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                if (!_priceHandlers.ContainsKey(exchangeQuote))
                    throw new ApplicationException("Specified exchange and quote does not exist.");

                PriceUpdateHandler del = _priceHandlers[exchangeQuote];
                _priceListeners[del].Remove(listener);
            }
        }
    }
}
