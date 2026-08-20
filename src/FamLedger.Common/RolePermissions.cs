using FamLedger.Domain.Enums;

namespace FamLedger.Common;

public static class RolePermissions
{
    public static bool CanManagePlan(FamilyMemberRole role) =>
        role is FamilyMemberRole.Head or FamilyMemberRole.Assistant;

    public static bool CanApproveJoinRequests(FamilyMemberRole role) =>
        role is FamilyMemberRole.Head or FamilyMemberRole.Assistant;

    public static bool CanManageFamilySettings(FamilyMemberRole role) =>
        role is FamilyMemberRole.Head;
}
