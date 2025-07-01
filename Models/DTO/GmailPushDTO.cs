using static CVexplorer.Controllers.GmailController;
using System.Text.Json.Serialization;

namespace CVexplorer.Models.DTO
{
    public class GmailPushDTO
    {
        [JsonPropertyName("message")]
        public PubSubMessage Message { get; set; }

        [JsonPropertyName("subscription")]
        public string Subscription { get; set; }
    }

    public class PubSubMessage
    {
        [JsonPropertyName("data")]
        public string Data { get; set; }


        [JsonPropertyName("messageId")]
        public string MessageId { get; set; }

        [JsonPropertyName("publishTime")]
        public string PublishTime { get; set; }

        [JsonPropertyName("message_id")]
        public string message_id { get; set; }

        [JsonPropertyName("publish_time")]
        public string publish_time { get; set; }
    }
}

