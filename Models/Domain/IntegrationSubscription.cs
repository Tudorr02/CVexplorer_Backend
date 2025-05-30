namespace CVexplorer.Models.Domain
{
    // Models/IntegrationSubscription.cs
    public class IntegrationSubscription
    {
        public long Id { get; set; }
        public int UserId { get; set; }    // FK către AspNetUsers
        public User User { get; set; }    // FK către AspNetUsers
        public string Provider { get; set; }    // "Gmail"
        public string LabelId { get; set; }    // ex. "me/label/{labelId}"

        public Guid PositionId { get; set; }    // FK către AspNetUsers
        public string SyncToken { get; set; }    // Gmail → historyId

        public string Email { get; set; }    // email-ul utilizatorului
        public DateTimeOffset ExpiresAt { get; set; }   // când expiră watch-ul
        public DateTimeOffset UpdatedAt { get; set; }

        public string SubscriptionName { get; set; }    // numele subscription-ului

        public required int RoundId { get; set; }    // FK către Round
        public Round Round { get; set; }    // FK către Round

        public int ProcessedCVs { get; set; } = 0;    // numărul de CV-uri procesate

    }

}
