global using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PocoBuilder.Tests;

public interface IArticle { int ArticleId { get; init; } }
public interface IPrice : IArticle { decimal Price { get; init; } }
public interface IName : IArticle { string Name { get; init; } }
public interface IDescription : IArticle { string Description { get; set; } }
public interface ICategory : IArticle { string Category { get; set; } }
