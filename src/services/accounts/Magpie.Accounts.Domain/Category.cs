namespace Magpie.Accounts.Domain;

/// <summary>
/// One entry in the two-level interest tree. A category with no parent is a
/// GROUP ("Technology"); a category with a parent is a TOPIC ("Programming").
/// Readers choose topics, never groups.
/// </summary>
public sealed class Category
{
    public int Id { get; private set; }

    /// <summary>A short, stable, address-friendly name, for example "ai-machine-learning".</summary>
    public string Slug { get; private set; }

    public string Name { get; private set; }

    public int? ParentId { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsTopic => ParentId is not null;

    public Category(int id, string slug, string name, int? parentId, int sortOrder)
    {
        Id = id;
        Slug = slug;
        Name = name;
        ParentId = parentId;
        SortOrder = sortOrder;
    }
}
