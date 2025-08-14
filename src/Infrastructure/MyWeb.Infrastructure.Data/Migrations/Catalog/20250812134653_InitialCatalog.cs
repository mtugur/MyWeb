using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyWeb.Persistence.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    MinEngine = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Controllers",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Controllers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Controllers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "catalog",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectVersions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AppliedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "catalog",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DataType = table.Column<byte>(type: "tinyint", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Scale = table.Column<double>(type: "float", nullable: true),
                    Offset = table.Column<double>(type: "float", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DriverRef = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LongString = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tags_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "catalog",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TagArchiveConfigs",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TagId = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    DeadbandAbs = table.Column<double>(type: "float", nullable: true),
                    DeadbandPercent = table.Column<double>(type: "float", nullable: true),
                    RetentionDays = table.Column<int>(type: "int", nullable: true),
                    RollupsJson = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagArchiveConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagArchiveConfigs_Tags_TagId",
                        column: x => x.TagId,
                        principalSchema: "catalog",
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Controllers_ProjectId",
                schema: "catalog",
                table: "Controllers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                schema: "catalog",
                table: "Projects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_ProjectId",
                schema: "catalog",
                table: "ProjectVersions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TagArchiveConfigs_TagId",
                schema: "catalog",
                table: "TagArchiveConfigs",
                column: "TagId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_ProjectId_Path",
                schema: "catalog",
                table: "Tags",
                columns: new[] { "ProjectId", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Controllers",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProjectVersions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TagArchiveConfigs",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Tags",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "catalog");
        }
    }
}
