using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations;

/// <inheritdoc />
public partial class SetActiveContextToFamilyIfAny : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // If a user belongs to a family budget but ActiveContext is null/personal,
        // switch them to that family (earliest membership if several).
        migrationBuilder.Sql(
            """
            UPDATE users AS u
            SET "ActiveContextId" = family."ContextId"
            FROM (
                SELECT DISTINCT ON (m."UserId")
                    m."UserId",
                    m."ContextId"
                FROM context_members AS m
                INNER JOIN budget_contexts AS bc ON bc."Id" = m."ContextId"
                WHERE bc."IsPersonal" = FALSE
                ORDER BY m."UserId", m."JoinedAt", m."Id"
            ) AS family
            WHERE u."Id" = family."UserId"
              AND (
                  u."ActiveContextId" IS NULL
                  OR NOT EXISTS (
                      SELECT 1
                      FROM budget_contexts AS active
                      WHERE active."Id" = u."ActiveContextId"
                        AND active."IsPersonal" = FALSE
                  )
              );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible data fix.
    }
}
