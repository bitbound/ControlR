using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_UserStorage : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "UserStorageItems",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          Value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_UserStorageItems", x => x.Id);
          table.ForeignKey(
                    name: "FK_UserStorageItems_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_UserStorageItems_Key_UserId",
        table: "UserStorageItems",
        columns: new[] { "Key", "UserId" },
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_UserStorageItems_UserId",
        table: "UserStorageItems",
        column: "UserId");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "UserStorageItems");
  }
}
