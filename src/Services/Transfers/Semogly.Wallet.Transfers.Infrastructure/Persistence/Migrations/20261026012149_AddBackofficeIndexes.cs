using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Semogly.Wallet.Transfers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackofficeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_transfers_CreatedAt",
                table: "transfers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_DestinationAccountId",
                table: "transfers",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_SourceAccountId",
                table: "transfers",
                column: "SourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transfers_Status_CreatedAt",
                table: "transfers",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transfers_CreatedAt",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "IX_transfers_DestinationAccountId",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "IX_transfers_SourceAccountId",
                table: "transfers");

            migrationBuilder.DropIndex(
                name: "IX_transfers_Status_CreatedAt",
                table: "transfers");
        }
    }
}
