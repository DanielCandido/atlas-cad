using System.Security.Cryptography;
using Atlas.Licensing;

namespace Atlas.AutoCAD.Adapter;

internal static class LicenseTrust
{
    public const string KeyId = "atlas-dev-2026-01";

    public static ECDsa CreateVerificationKey() => ECDsa.Create(LicenseCrypto.PublicParameters(
        "MxdoSGOppcu6dCTEUCHZN4HmW9VVqXcbsU76-GoCRsw",
        "uMucqjofTzEbPD7XvY7FlnI0KFIT8UbIZSiRsmp6z_I"));
}
