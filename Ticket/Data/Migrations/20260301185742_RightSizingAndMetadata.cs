using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket.Data.Migrations
{
    /// <inheritdoc />
    public partial class RightSizingAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReferenceCode",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionNormalized",
                table: "Tickets",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Metadata_Channel",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientEmailNormalized",
                table: "Tickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientNameNormalized",
                table: "Tickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceCodeNormalized",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterEmailNormalized",
                table: "Tickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterNameNormalized",
                table: "Tickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleNormalized",
                table: "Tickets",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TitleNormalized",
                table: "Tickets",
                column: "TitleNormalized");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_TitleNormalized",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DescriptionNormalized",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Metadata_Channel",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RecipientEmailNormalized",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RecipientNameNormalized",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "ReferenceCodeNormalized",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RequesterEmailNormalized",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RequesterNameNormalized",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TitleNormalized",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceCode",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
