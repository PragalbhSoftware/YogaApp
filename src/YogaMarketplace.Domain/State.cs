namespace YogaMarketplace.Domain;

public class State
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
