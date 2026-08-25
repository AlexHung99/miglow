using GongWei.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GongWei.Infrastructure.Persistence.Migrations;

/// <summary>
/// Makes all narrative character-application fields optional. The columns remain
/// non-null with empty-string defaults; only their submit-time minimum lengths are
/// removed. Birth date remains in the model for birth records, but is not collected
/// by the application flow.
/// </summary>
[DbContext(typeof(GongWeiDbContext))]
[Migration("20260826090000_OptionalCharacterProfileFields")]
public sealed class OptionalCharacterProfileFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var constraint in Constraints)
        {
            migrationBuilder.DropCheckConstraint(
                name: constraint.Name,
                schema: "game",
                table: "character_applications");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var constraint in Constraints)
        {
            migrationBuilder.AddCheckConstraint(
                name: constraint.Name,
                schema: "game",
                table: "character_applications",
                sql: constraint.Sql);
        }
    }

    private static readonly (string Name, string Sql)[] Constraints =
    [
        ("ck_ca_appearance_len", "status = 'draft' OR char_length(appearance) >= 60"),
        ("ck_ca_personality_len", "status = 'draft' OR char_length(personality) >= 50"),
        ("ck_ca_strengths_len", "status = 'draft' OR char_length(strengths) >= 50"),
        ("ck_ca_weaknesses_len", "status = 'draft' OR char_length(weaknesses) >= 50"),
        ("ck_ca_likes_len", "status = 'draft' OR char_length(likes) >= 50"),
        ("ck_ca_dislikes_len", "status = 'draft' OR char_length(dislikes) >= 50"),
        ("ck_ca_biography_len", "status = 'draft' OR char_length(biography) >= 200")
    ];
}
