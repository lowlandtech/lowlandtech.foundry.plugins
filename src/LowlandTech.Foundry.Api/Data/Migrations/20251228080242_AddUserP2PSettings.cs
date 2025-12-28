using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LowlandTech.Foundry.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserP2PSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserP2PSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    PeerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    P2PDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EnableLanDiscovery = table.Column<bool>(type: "boolean", nullable: false),
                    EnableWanDiscovery = table.Column<bool>(type: "boolean", nullable: false),
                    RequirePeerVerification = table.Column<bool>(type: "boolean", nullable: false),
                    AutoAcceptTrustedPeers = table.Column<bool>(type: "boolean", nullable: false),
                    EnablePluginSharing = table.Column<bool>(type: "boolean", nullable: false),
                    AutoDownloadFromTrusted = table.Column<bool>(type: "boolean", nullable: false),
                    RequireSignedPlugins = table.Column<bool>(type: "boolean", nullable: false),
                    TrustedPeerIds = table.Column<string>(type: "text", nullable: true),
                    BlockedPeerIds = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserP2PSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserP2PSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserP2PSettings_UserId",
                table: "UserP2PSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserP2PSettings");
        }
    }
}
