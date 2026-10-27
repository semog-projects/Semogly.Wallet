using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Semogly.Wallet.Transfers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaTimeouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FraudCheckTimeoutTokenId",
                table: "transfer_sagas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LedgerTimeoutTokenId",
                table: "transfer_sagas",
                type: "uuid",
                nullable: true);

            // Agendador persistente dos timeouts (Quartz)
            migrationBuilder.Sql(QuartzSchema.CreateTablesSql());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(QuartzSchema.DropTablesSql());

            migrationBuilder.DropColumn(
                name: "FraudCheckTimeoutTokenId",
                table: "transfer_sagas");

            migrationBuilder.DropColumn(
                name: "LedgerTimeoutTokenId",
                table: "transfer_sagas");
        }
    }
}
