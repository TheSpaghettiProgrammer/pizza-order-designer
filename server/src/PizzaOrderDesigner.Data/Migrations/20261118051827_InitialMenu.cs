using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PizzaOrderDesigner.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RuleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    MinSubtotalExclusive = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ExclusionGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sizes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "topping_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topping_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "toppings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toppings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_toppings_topping_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "topping_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "promotions",
                columns: new[] { "Id", "Amount", "Code", "ExclusionGroup", "IsActive", "MinSubtotalExclusive", "Percent", "RuleType" },
                values: new object[,]
                {
                    { 1, null, "ILOVEPIZZA", "order-discount", true, null, 0.10m, "PercentOffOrder" },
                    { 2, 5.00m, "SAVE5", "order-discount", true, 20.00m, null, "AmountOffOverThreshold" }
                });

            migrationBuilder.InsertData(
                table: "sizes",
                columns: new[] { "Id", "BasePrice", "Code", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, 5.00m, "small", "Small", 1 },
                    { 2, 7.00m, "medium", "Medium", 2 },
                    { 3, 12.00m, "large", "Large", 3 }
                });

            migrationBuilder.InsertData(
                table: "topping_categories",
                columns: new[] { "Id", "Code", "Name", "Rate", "SortOrder" },
                values: new object[,]
                {
                    { 1, "basic", "Basic", 0.08m, 1 },
                    { 2, "premium", "Premium", 0.11m, 2 }
                });

            migrationBuilder.InsertData(
                table: "toppings",
                columns: new[] { "Id", "CategoryId", "Code", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, 1, "pepperoni", "Pepperoni", 1 },
                    { 2, 1, "sausage", "Sausage", 2 },
                    { 3, 1, "extra-cheese", "Extra cheese", 3 },
                    { 4, 1, "onions", "Onions", 4 },
                    { 5, 1, "black-olives", "Black olives", 5 },
                    { 6, 1, "green-peppers", "Green peppers", 6 },
                    { 7, 2, "bacon", "Bacon", 7 },
                    { 8, 2, "chicken", "Chicken", 8 },
                    { 9, 2, "garlic", "Garlic", 9 },
                    { 10, 2, "artichoke-hearts", "Artichoke hearts", 10 },
                    { 11, 2, "sun-dried-tomatoes", "Sun-dried tomatoes", 11 },
                    { 12, 2, "pineapple", "Pineapple", 12 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_promotions_Code",
                table: "promotions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sizes_Code",
                table: "sizes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_topping_categories_Code",
                table: "topping_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_toppings_CategoryId",
                table: "toppings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_toppings_Code",
                table: "toppings",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotions");

            migrationBuilder.DropTable(
                name: "sizes");

            migrationBuilder.DropTable(
                name: "toppings");

            migrationBuilder.DropTable(
                name: "topping_categories");
        }
    }
}
