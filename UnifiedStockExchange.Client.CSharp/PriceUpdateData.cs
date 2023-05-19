using System.Text.Json.Serialization;

namespace UnifiedStockExchange.Client.CSharp
{
    public class PriceUpdateData
    {
        [JsonPropertyName("time")]
        public long Time { get; set; }
        
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
    }
}