using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Magpie.Collector.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCollector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CategoryHint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FetchIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastFetchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextFetchAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    ETag = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "collected_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CategoryHint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CollectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collected_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_collected_items_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_collected_items_source_external",
                table: "collected_items",
                columns: new[] { "SourceId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sources_Address",
                table: "sources",
                column: "Address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sources_due",
                table: "sources",
                columns: new[] { "Enabled", "NextFetchAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collected_items");

            migrationBuilder.DropTable(
                name: "sources");
        }
    }
}
