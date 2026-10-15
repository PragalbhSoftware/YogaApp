namespace YogaMarketplace.Domain;

public class City
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid StateId { get; set; }
    public State? State { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string Slug { get; set; } = "";
}
