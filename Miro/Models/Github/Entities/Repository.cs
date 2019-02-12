namespace Miro.Models.Github.Entities
{
    public class Repository
    {
        public User Owner { get; set; }
        public string Name { get; set; }
        public bool Fork { get; set; }
    }
}