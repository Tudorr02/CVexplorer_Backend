namespace CVexplorer.Models.DTO
{
    public class CvEvaluationDTO
    {
        public byte[] FileData { get; set; } = null!;

        public int Score { get; set; } = 0;
        public CvEvaluationResultDTO Evaluation { get; set; } = new CvEvaluationResultDTO();
    }
}
