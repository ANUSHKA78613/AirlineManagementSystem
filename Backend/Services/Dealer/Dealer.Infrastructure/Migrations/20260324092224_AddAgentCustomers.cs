using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CommissionRate",
                table: "DealerAgents",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.CreateTable(
                name: "AgentCustomers",
                columns: table => new
                {
                    AgentCustomerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentCustomers", x => x.AgentCustomerId);
                    table.ForeignKey(
                        name: "FK_AgentCustomers_DealerAgents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "DealerAgents",
                        principalColumn: "AgentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentCustomers_AgentId_CustomerId",
                table: "AgentCustomers",
                columns: new[] { "AgentId", "CustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentCustomers");

            migrationBuilder.AlterColumn<decimal>(
                name: "CommissionRate",
                table: "DealerAgents",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");
        }
    }
}
