namespace Miro.Models.Github.Entities
{
    public class Head
    {
         public string Ref { get; set; }
         public string Sha { get; set; }
         public Repository Repo { get; set; }
    }
}