namespace CVexplorer.Models.DTO
{
    public class RoundStageListDTO
    {
       
        public string Name { get; set; }
        public int Ordinal { get; set; }

        public List<RoundEntryListDTO> Entries { get; set; }


    }
}
