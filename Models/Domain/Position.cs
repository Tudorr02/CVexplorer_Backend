namespace CVexplorer.Models.Domain
{
    public class Position
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public int DepartmentId { get; set; }
        public required Department Department { get; set; }

        // 🔧 Skill-uri obligatorii (tehnice sau nu)
        public List<string> RequiredSkills { get; set; } = new();

        // ➕ Skill-uri opționale (bonus la scor)
        public List<string> NiceToHave { get; set; } = new();

        // 🌐 Limbi necesare: ex. {"English": "B2", "French": "A2"}
        public Dictionary<string, string> Languages { get; set; } = new();

        // 📜 Certificări necesare
        public List<string> Certifications { get; set; } = new();

        // 📝 Responsabilități (pentru scor semantic sau NLP)
        public List<string> Responsibilities { get; set; } = new();

        // 📆 Experiență minimă necesară (în luni)
        public int? MinimumExperienceMonths { get; set; }

        // 📊 Nivelul poziției: intern, junior, mid, senior, lead
        public string? Level { get; set; }

        // 🎓 Nivel minim de educație: ex. "Bachelor", "Master", "High School"
        public string? MinimumEducationLevel { get; set; }


    }
}
