namespace CVexplorer.Models.DTO
{
    public class CvEvaluationDTO
    {
        public string FileData { get; set; }
        public string cvPublicId { get; set; }
        public int Score { get; set; } = 0;
        public CvEvaluationResultDTO Evaluation { get; set; } = new CvEvaluationResultDTO();
    }
}
