using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoxToBox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedIdentityData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"), "c8f2c4db-6a43-4f5e-8d8a-0d8f1b5d9b8a", "ADMIN", "ADMIN" });

            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Created", "Email", "EmailConfirmed", "FirstName", "LastName", "LockoutEnabled", "LockoutEnd", "Modified", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"), 0, "a4e5b6c7-d8e9-4f0a-b1c2-d3e4f5a6b7c8", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "moyumbnm@hotmail.com", true, "Sezgin", "Sahin", false, null, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "MOYUMBNM@HOTMAIL.COM", "SEZ", "AQAAAAIAAYagAAAAEI7ElNLhKrImzrJG+Y8B9IktAAPhNj0ea9M+32wIJRCUUKfh5QVg+xOtTbtFhnqHeQ==", null, false, "d1c7b2df-7c5c-44a7-8c6c-4f5a8d1c9e7b", false, "Sez" });

            migrationBuilder.InsertData(
                table: "AspNetUserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"), new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"), new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6") });

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa7"));

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: new Guid("3fa85f64-5717-4562-b3fc-2c963f66afa6"));
        }
    }
}
