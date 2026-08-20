using System.Security.Cryptography;
using System.Text;

namespace FamLedger.Common;

public static class InviteCodeGenerator
{
    public static string Generate() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
