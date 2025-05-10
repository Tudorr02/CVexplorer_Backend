namespace CVexplorer.Models.DTO
{
    public class GmailPushJobDTO
    {
        public long InterogationSubscriptionId { get; set; }
        public string EmailAddress { get; set; } = string.Empty;
    }
}
