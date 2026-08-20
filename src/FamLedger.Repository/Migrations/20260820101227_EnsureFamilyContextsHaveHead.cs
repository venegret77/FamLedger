using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamLedger.Repository.Migrations;

/// <inheritdoc />
public partial class EnsureFamilyContextsHaveHead : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // FamilyMemberRole.Head = 0. Fix family contexts that somehow have no Head:
        // promote the earliest member (by JoinedAt, then Id) to Head.
        migrationBuilder.Sql(
            """
            UPDATE context_members AS cm
            SET "Role" = 0
            WHERE cm."Id" IN (
                SELECT DISTINCT ON (m."ContextId") m."Id"
                FROM context_members AS m
                INNER JOIN budget_contexts AS bc ON bc."Id" = m."ContextId"
                WHERE bc."IsPersonal" = FALSE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM context_members AS h
                      WHERE h."ContextId" = m."ContextId"
                        AND h."Role" = 0
                  )
                ORDER BY m."ContextId", m."JoinedAt", m."Id"
            );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible data fix.
    }
}
