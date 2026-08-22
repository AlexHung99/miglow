using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GongWei.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocalAdminCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_credentials",
                schema: "game",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    version = table.Column<long>(type: "bigint", rowVersion: true, nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_credentials", x => x.user_id);
                    table.CheckConstraint("ck_admin_credentials_failed", "failed_attempts >= 0");
                    table.CheckConstraint("ck_admin_credentials_username", "char_length(btrim(username)) BETWEEN 3 AND 64");
                    table.CheckConstraint("ck_admin_credentials_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_admin_credentials_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "game",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_credentials_username",
                schema: "game",
                table: "admin_credentials",
                column: "username");

            // Case-insensitive uniqueness, which EF cannot express: without it "Admin" and
            // "admin" would be two accounts, which a human reading an audit log would read
            // as one.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ux_admin_credentials_username_lower
                    ON game.admin_credentials (lower(username));
                """);

            // Same version/updated_at behaviour as every other versioned table, so the EF
            // concurrency token works here exactly as it does elsewhere.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER tr_admin_credentials_touch
                    BEFORE UPDATE ON game.admin_credentials
                    FOR EACH ROW EXECUTE FUNCTION game.touch_updated_at();
                """);

            migrationBuilder.Sql(
                """
                COMMENT ON TABLE game.admin_credentials IS
                    'Local username/password for the control back office. Deliberately not part of schema_v1.1.sql: backend_spec_v1.1 section 155 has admin identity arriving through LINE Login. Added on the operator''s explicit instruction.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS tr_admin_credentials_touch ON game.admin_credentials;");

            migrationBuilder.DropTable(
                name: "admin_credentials",
                schema: "game");
        }
    }
}
