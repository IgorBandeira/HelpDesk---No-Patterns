namespace HelpDesk.Models
{
    public class CategoryModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";      // ex: "Infra > Rede"
        public int? ParentId { get; set; }
        public CategoryModel? Parent { get; set; }
        public ICollection<CategoryModel> Children { get; set; } = new List<CategoryModel>();
    }

}
