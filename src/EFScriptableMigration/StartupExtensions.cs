using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration;

public static class StartupExtensions
{
    public static async Task<MigrationReport> Start(this DbMigration migrationConfig)
    {
        var sqlMigration = new DbScriptedMigration(migrationConfig);
        var result = await sqlMigration.Run();
        return result;
    }

    internal static string GetSHA256(this string input)
    {
        using var crypto = System.Security.Cryptography.SHA256.Create();
        var buffer = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = crypto.ComputeHash(buffer);
        var result = string.Join(string.Empty, from b in hash select b.ToString("X2"));
        return result;
    }
}
