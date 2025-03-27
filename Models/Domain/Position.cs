using CVexplorer.Enums;

namespace CVexplorer.Models.Domain
{
    public class Position
    {
        public required Guid Id { get; set; }

        public required string PublicId { get; set; } 
        public required string Name { get; set; }
        public required int DepartmentId { get; set; }
        public required Department Department { get; set; }

        // 🔧 Skill-uri obligatorii (tehnice sau nu)
        public List<string> RequiredSkills { get; set; } = [];

        // ➕ Skill-uri opționale (bonus la scor)
        public List<string> NiceToHave { get; set; } = [];

        // 🌐 Limbi necesare: ex. {"English": "B2", "French": "A2"}
        public List<string> Languages { get; set; } = [];

        // 📜 Certificări necesare
        public List<string> Certification { get; set; } = [];

        // 📝 Responsabilități (pentru scor semantic sau NLP)
        public List<string> Responsibilities { get; set; } = [];

        // 📆 Experiență minimă necesară (în luni)
        public int MinimumExperienceMonths { get; set; } = 0;

        // 📊 Nivelul poziției: intern, junior, mid, senior, lead
        public PositionLevel Level { get; set; } = PositionLevel.Intern;

        // 🎓 Nivel minim de educație: ex. "Bachelor", "Master", "High School"
        public EducationLevel MinimumEducationLevel { get; set; } = EducationLevel.HighSchool;


    }
}
