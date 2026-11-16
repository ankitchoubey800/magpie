using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Magpie.Accounts.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInterests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_categories_categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_interests",
                columns: table => new
                {
                    ReaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_interests", x => new { x.ReaderId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_user_interests_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_interests_users_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "Id", "Name", "ParentId", "Slug", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Technology", null, "technology", 1 },
                    { 2, "Science", null, "science", 2 },
                    { 3, "Business", null, "business", 3 },
                    { 4, "Culture", null, "culture", 4 },
                    { 5, "Lifestyle", null, "lifestyle", 5 },
                    { 6, "World", null, "world", 6 },
                    { 101, "Programming", 1, "programming", 1 },
                    { 102, "AI & Machine Learning", 1, "ai-machine-learning", 2 },
                    { 103, "Cloud & DevOps", 1, "cloud-devops", 3 },
                    { 104, "Security", 1, "security", 4 },
                    { 201, "Space", 2, "space", 1 },
                    { 202, "Physics", 2, "physics", 2 },
                    { 203, "Biology & Medicine", 2, "biology-medicine", 3 },
                    { 204, "Climate & Environment", 2, "climate-environment", 4 },
                    { 301, "Startups", 3, "startups", 1 },
                    { 302, "Finance & Markets", 3, "finance-markets", 2 },
                    { 303, "Careers", 3, "careers", 3 },
                    { 304, "Product & Design", 3, "product-design", 4 },
                    { 401, "Books", 4, "books", 1 },
                    { 402, "Film & TV", 4, "film-tv", 2 },
                    { 403, "Music", 4, "music", 3 },
                    { 404, "Gaming", 4, "gaming", 4 },
                    { 501, "Food & Cooking", 5, "food-cooking", 1 },
                    { 502, "Health & Fitness", 5, "health-fitness", 2 },
                    { 503, "Travel", 5, "travel", 3 },
                    { 504, "Personal Finance", 5, "personal-finance", 4 },
                    { 601, "World News", 6, "world-news", 1 },
                    { 602, "India", 6, "india", 2 },
                    { 603, "Sport", 6, "sport", 3 },
                    { 604, "Education", 6, "education", 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_ParentId",
                table: "categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_Slug",
                table: "categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_interests_category",
                table: "user_interests",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_interests");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
