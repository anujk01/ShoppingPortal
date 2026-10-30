using ShoppingPortal.Application;

namespace ShoppingPortal.Infrastructure;

public sealed class CatalogService : ICatalogService
{
    private static readonly CategoryDto[] Categories =
    [
        new("electronics", "Electronics", "⌁", "#e8efff"),
        new("fashion", "Fashion", "✳", "#fce8e8"),
        new("home", "Home & Living", "⌂", "#e6f3e9"),
        new("beauty", "Beauty", "✿", "#f8eafa"),
        new("appliances", "Appliances", "▤", "#fff0df"),
        new("toys", "Toys & More", "☆", "#e4f4f2"),
        new("grocery", "Grocery", "❋", "#f3f1dd"),
        new("sports", "Sports", "◉", "#e9e8f8")
    ];

    private static readonly ProductDto[] Products =
    [
        new("p1", "Studio Wireless Headphones", "Immersive sound, all-day comfort", 89.00m, 129.00m, "🎧", "electronics", "BESTSELLER"),
        new("p2", "Everyday Carry Backpack", "Room for work, play and everything", 54.00m, 72.00m, "🎒", "fashion", "−25%"),
        new("p3", "Minimal Ceramic Table Set", "Thoughtful pieces for slow mornings", 38.00m, null, "🍵", "home", "NEW"),
        new("p4", "Daily Ritual Skincare Kit", "A simple routine, beautifully made", 42.00m, 58.00m, "🧴", "beauty", "−28%"),
        new("p5", "Compact Espresso Maker", "Cafe-quality coffee, right at home", 119.00m, 149.00m, "☕", "appliances", "POPULAR"),
        new("p6", "Weekend Trail Sneakers", "Lightweight grip for wherever you go", 76.00m, null, "👟", "fashion", "JUST IN")
    ];

    public IReadOnlyList<CategoryDto> GetCategories() => Categories;
    public IReadOnlyList<ProductDto> GetProducts(string? categoryId) =>
        string.IsNullOrWhiteSpace(categoryId) ? Products : Products.Where(product => product.CategoryId == categoryId).ToArray();
}
