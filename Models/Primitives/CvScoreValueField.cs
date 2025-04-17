namespace CVexplorer.Models.Primitives
{
    public class CvScoreValueField<TValue>
    {
        public TValue Value { get; set; } = default!;
        public double Score { get; set; } = 0;
    }
}
