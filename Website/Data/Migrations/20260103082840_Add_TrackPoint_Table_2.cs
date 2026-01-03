using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_TrackPoint_Table_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackPoint_Rides_RideId",
                table: "TrackPoint");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackPoint",
                table: "TrackPoint");

            migrationBuilder.RenameTable(
                name: "TrackPoint",
                newName: "TrackPoints");

            migrationBuilder.RenameIndex(
                name: "IX_TrackPoint_RideId",
                table: "TrackPoints",
                newName: "IX_TrackPoints_RideId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackPoints",
                table: "TrackPoints",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackPoints_Rides_RideId",
                table: "TrackPoints",
                column: "RideId",
                principalTable: "Rides",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackPoints_Rides_RideId",
                table: "TrackPoints");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackPoints",
                table: "TrackPoints");

            migrationBuilder.RenameTable(
                name: "TrackPoints",
                newName: "TrackPoint");

            migrationBuilder.RenameIndex(
                name: "IX_TrackPoints_RideId",
                table: "TrackPoint",
                newName: "IX_TrackPoint_RideId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackPoint",
                table: "TrackPoint",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackPoint_Rides_RideId",
                table: "TrackPoint",
                column: "RideId",
                principalTable: "Rides",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
