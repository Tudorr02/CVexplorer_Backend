namespace CVexplorer.Models.DTO
{
    public class PushJobDTO
    {
        /// <summary>
        /// "Gmail" sau "Outlook"
        /// </summary>
        public string Provider { get; set; } = "";

        /// <summary>
        /// Pentru Gmail: IntegrationSubscription.Id
        /// Pentru Outlook: Graph SubscriptionId (GUID)
        /// </summary>
        public string SubscriptionId { get; set; } = "";

        /// <summary>
        /// Pentru Gmail: LabelId
        /// Pentru Outlook: FolderId
        /// </summary>
        public string ResourceId { get; set; } = "";

        /// <summary>
        /// Pentru Outlook: MessageId
        /// Pentru Gmail: nu se folosește (poate fi lăsat gol)
        /// </summary>
        public string MessageId { get; set; } = "";
    }
}
