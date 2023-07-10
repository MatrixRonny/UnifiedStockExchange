using ServiceStack;
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

        // [exchangeQuote] = incomingListener
        Dictionary<string, PriceUpdateHandler> _priceListeners = new Dictionary<string, PriceUpdateHandler>();

        // [incomingListener][index] = forwardHandler
        Dictionary<PriceUpdateHandler, List<PriceUpdateHandler>> _priceForwarders = new Dictionary<PriceUpdateHandler, List<PriceUpdateHandler>>();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> ActiveExchangeQuotes =>
            _priceQuotes.ToImmutableDictionary(it => it.Key, it => (IReadOnlyList<string>)it.Value);

        public event Action<PriceUpdateHandler>? ForwardingHandlerRemoved;

        public PriceUpdateHandler CreateIncomingListener(string exchangeName, ValueTuple<string, string> tradingPair)
        {
            lock (_priceQuotes)
            lock (_priceListeners)
            lock (_priceForwarders)
            {
                if (_priceQuotes.ContainsKey(exchangeName) && _priceQuotes[exchangeName].Contains(tradingPair.ToPairString()))
                    throw new ApplicationException("A listener has already been registered for that exchange and quote.");

                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                switch (exchangeName)
                {
                    case "CoinMarketCap":
                    {
                        ValueReference<PriceUpdateHandler> valRef = new();
                        PriceUpdateHandler handler = (tradingPair, time, price, amount) =>
                        {
                            List<PriceUpdateHandler> forwardList = _priceForwarders[valRef.Value!];
                            lock(forwardList)
                            {
                                Parallel.ForEach(forwardList, update =>
                                {
                                    try
                                    {
                                        update(tradingPair, time, price, amount);
                                    }
                                    catch
                                    {
                                    }
                                });
                            }
                        };
                        valRef.Value = handler;

                        if (!_priceQuotes.ContainsKey(exchangeName))
                        {
                            _priceQuotes[exchangeName] = new();
                        }
                        _priceQuotes[exchangeName].Add(tradingPair.ToPairString());
                        _priceListeners[exchangeQuote] = handler;
                        _priceForwarders[handler] = new List<PriceUpdateHandler>();

                        return handler;
                    }

                    default:
                        throw new ApplicationException("Specified exchange does not exist.");
                }
            }
        }

        public void RemoveIncomingListener(string exchangeName, ValueTuple<string, string> tradingPair)
        {
            List<PriceUpdateHandler> forwardHandlers;

            lock (_priceQuotes)
            lock (_priceListeners)
            lock (_priceForwarders)
            {
                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);

                PriceUpdateHandler incomingListener = _priceListeners[exchangeQuote];
                _priceForwarders.TryRemove(incomingListener, out forwardHandlers);
                _priceListeners.Remove(exchangeQuote);

                _priceQuotes[exchangeName].Remove(tradingPair.ToPairString());
                if (_priceQuotes[exchangeName].Count == 0)
                {
                    _priceQuotes.Remove(exchangeName);
                }
            }

            foreach (PriceUpdateHandler forwarder in forwardHandlers)
            {
                ForwardingHandlerRemoved?.Invoke(forwarder);
            }
        }
        public void AddForwardHandler(string exchangeName, ValueTuple<string, string> tradingPair, PriceUpdateHandler priceForwarder)
        {
            lock (_priceListeners)
            lock (_priceForwarders)
            {
                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                if (!_priceListeners.ContainsKey(exchangeQuote))
                    throw new ApplicationException("Specified exchange and trading pair does not exist.");

                PriceUpdateHandler priceListener = _priceListeners[exchangeQuote];
                _priceForwarders[priceListener].Add(priceForwarder);
            }
        }

        public void RemoveForwardHandler(string exchangeName, ValueTuple<string, string> tradingPair, PriceUpdateHandler priceForwarder)
        {
            lock (_priceListeners)
            lock (_priceForwarders)
            {
                string exchangeQuote = tradingPair.ToExchangeQuote(exchangeName);
                if (!_priceListeners.ContainsKey(exchangeQuote))
                    throw new ApplicationException("Specified exchange and quote does not exist.");

                PriceUpdateHandler del = _priceListeners[exchangeQuote];
                _priceForwarders[del].Remove(priceForwarder);
            }
        }
    }
}
