namespace Magpie.Accounts.Domain;

/// <summary>
/// The first category list: 6 groups with 4 topics each (30 entries).
/// This is a PRODUCT decision, not a technical one: too many choices
/// overwhelm a new reader, too few make every feed look the same.
///
/// The ids are written out by hand and must never be reused or renumbered,
/// because readers' choices refer to them. Topics are numbered group x 100 + n.
/// </summary>
public static class StarterCategories
{
    public static readonly IReadOnlyList<Category> All =
    [
        new(1, "technology", "Technology", null, 1),
        new(101, "programming", "Programming", 1, 1),
        new(102, "ai-machine-learning", "AI & Machine Learning", 1, 2),
        new(103, "cloud-devops", "Cloud & DevOps", 1, 3),
        new(104, "security", "Security", 1, 4),

        new(2, "science", "Science", null, 2),
        new(201, "space", "Space", 2, 1),
        new(202, "physics", "Physics", 2, 2),
        new(203, "biology-medicine", "Biology & Medicine", 2, 3),
        new(204, "climate-environment", "Climate & Environment", 2, 4),

        new(3, "business", "Business", null, 3),
        new(301, "startups", "Startups", 3, 1),
        new(302, "finance-markets", "Finance & Markets", 3, 2),
        new(303, "careers", "Careers", 3, 3),
        new(304, "product-design", "Product & Design", 3, 4),

        new(4, "culture", "Culture", null, 4),
        new(401, "books", "Books", 4, 1),
        new(402, "film-tv", "Film & TV", 4, 2),
        new(403, "music", "Music", 4, 3),
        new(404, "gaming", "Gaming", 4, 4),

        new(5, "lifestyle", "Lifestyle", null, 5),
        new(501, "food-cooking", "Food & Cooking", 5, 1),
        new(502, "health-fitness", "Health & Fitness", 5, 2),
        new(503, "travel", "Travel", 5, 3),
        new(504, "personal-finance", "Personal Finance", 5, 4),

        new(6, "world", "World", null, 6),
        new(601, "world-news", "World News", 6, 1),
        new(602, "india", "India", 6, 2),
        new(603, "sport", "Sport", 6, 3),
        new(604, "education", "Education", 6, 4),
    ];
}
