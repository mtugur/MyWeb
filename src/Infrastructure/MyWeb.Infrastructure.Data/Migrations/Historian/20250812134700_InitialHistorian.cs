using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyWeb.Persistence.Migrations.Historian
{
    /// <inheritdoc />
    public partial class InitialHistorian : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hist");

            migrationBuilder.CreateTable(
                name: "Samples",
                schema: "hist",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false),
                    Utc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    DataType = table.Column<byte>(type: "tinyint", nullable: false),
                    ValueNumeric = table.Column<double>(type: "float", nullable: true),
                    ValueText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValueBool = table.Column<bool>(type: "bit", nullable: true),
                    Quality = table.Column<short>(type: "smallint", nullable: false),
                    Source = table.Column<byte>(type: "tinyint", nullable: true),
                    MonthKey = table.Column<int>(type: "int", nullable: false, computedColumnSql: "(YEAR([Utc])*100 + MONTH([Utc]))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Samples", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_MonthKey",
                schema: "hist",
                table: "Samples",
                column: "MonthKey")
                .Annotation("SqlServer:Include", new[] { "TagId", "Utc", "ValueNumeric", "Quality" });

            migrationBuilder.CreateIndex(
                name: "IX_Samples_TagId_Utc",
                schema: "hist",
                table: "Samples",
                columns: new[] { "TagId", "Utc" })
                .Annotation("SqlServer:Clustered", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Samples",
                schema: "hist");
        }
    }
}
