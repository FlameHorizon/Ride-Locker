using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_TrackPoint_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackPoint",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RideId = table.Column<int>(type: "INTEGER", nullable: false),
                    Elevation = table.Column<double>(type: "REAL", nullable: false),
                    Time = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Hdop = table.Column<double>(type: "REAL", nullable: false),
                    Speed = table.Column<double>(type: "REAL", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackPoint", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackPoint_Rides_RideId",
                        column: x => x.RideId,
                        principalTable: "Rides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackPoint_RideId",
                table: "TrackPoint",
                column: "RideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackPoint");
        }
    }
}
