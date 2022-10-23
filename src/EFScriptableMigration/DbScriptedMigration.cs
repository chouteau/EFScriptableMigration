using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Data.SqlClient;

namespace EFScriptableMigration;

internal class DbScriptedMigration
{
    internal DbScriptedMigration(DbMigration dbMigrationConfig)
    {
        this.DbMigrationConfig = dbMigrationConfig;
    }

    protected DbMigration DbMigrationConfig { get; set; }

    public async Task<MigrationReport> Run()
    {
        var scriptList = new Dictionary<string, List<SqlScript>>();
        if (DbMigrationConfig.ExtendedScripts.Any())
        {
            foreach (var item in DbMigrationConfig.ExtendedScripts)
            {
                if (!scriptList.ContainsKey(item.Key))
                {
                    scriptList.Add(item.Key, item.Value);
                }
            }
        }

        if (DbMigrationConfig.ApplyEmbededScripts)
        {
            if (DbMigrationConfig.EmbededTypeReference == null)
            {
                throw new Exception("EmbededTypeReference is required");
            }
            var embededScriptList = GetScriptListFromEmbededResources(DbMigrationConfig.EmbededTypeReference);
            foreach (var item in embededScriptList)
            {
                if (!scriptList.ContainsKey(item.Key))
                {
                    scriptList.Add(item.Key, item.Value);
                }
            }
        }

        if (!scriptList.Any())
        {
            return new MigrationReport();
        }

        var report = new MigrationReport();
        foreach (var item in scriptList)
        {
            if (string.IsNullOrWhiteSpace(DbMigrationConfig.SchemaName))
            {
                throw new Exception("Schema name in configuration required");
            }
            if (DbMigrationConfig.SchemaName != "All"
                && !item.Key.Equals($"__schema_{DbMigrationConfig.SchemaName}", StringComparison.InvariantCultureIgnoreCase))
            {
                // On applique que le schema en cours ou tous pour les tests
                continue;
            }
            var lastSchema = await GetLastSchema(item.Key);
            var newVersion = await ApplyScripts(lastSchema, item.Value, item.Key);
            report.LastSchema.Add(item.Key, lastSchema.Version);
            if (newVersion > 0)
            {
                report.AppliedScriptList.Add(item.Key, newVersion);
            }
        }
        return report;
    }

    internal virtual Dictionary<string, List<SqlScript>> GetScriptListFromEmbededResources(Type assemblyType)
    {
        var assembly = System.Reflection.Assembly.GetAssembly(assemblyType);
        var embededList = assembly.GetManifestResourceNames();
        var result = new Dictionary<string, List<SqlScript>>();

        foreach (var script in embededList.OrderBy(i => i))
        {
            var match = System.Text.RegularExpressions.Regex.Match(script, @"(?<schema>[^\.]+).(?<version>\d+)-(?<name>[^\.]+).sql$");
            if (!match.Success)
            {
                continue;
            }
            var version = match.Groups["version"].Value;
            var name = match.Groups["name"].Value;
            var schema = $"__schema_{match.Groups["schema"].Value}";
            string content = null;
            using (var stream = assembly.GetManifestResourceStream(script))
            {
                if (stream == null)
                {
                    continue;
                }
                var buffer = new byte[stream.Length];

                // En cas d'erreur, bien vérifier que le script est encodé en UTF-8 with BOM
                stream.Read(buffer, 0, buffer.Length);
                content = System.Text.Encoding.UTF8.GetString(buffer);
                content = content.Substring(1);
            }

            if (!result.ContainsKey(schema))
            {
                result.Add(schema, new List<SqlScript>());
            }

            result[schema].Add(new SqlScript()
            {
                Version = Convert.ToInt32(version),
                Name = script,
                Content = content,
                Hash = content.GetSHA256()
            });
        }

        return result;
    }

    private async Task CreateSchemaTableIfNotExists(string schemaName)
    {
        using (var cnx = new SqlConnection(DbMigrationConfig.ConnectionString))
        {
            var cmd = cnx.CreateCommand();
            cmd.CommandText = @"
if not exists(select name from sysobjects where name = '@SchemaTableName' and xtype = 'U')
Begin
	Create table dbo.[@SchemaTableName] (
		Id int identity(1,1) not null
		, Version int not null
		, CreationDate DateTime2 not null
		, Name varchar(100) not null
		, Script varchar(max) not null
	    , Hash varchar(100) not null
	)
	alter table dbo.[@SchemaTableName] add constraint PK_@SchemaTableName_Id primary key (Id)
	Create unique index IX_@SchemaTableName_VersionHash on [@SchemaTableName](Version, Hash)
End
".Replace("@SchemaTableName", schemaName);

            await cnx.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
            await cnx.CloseAsync();
        }
    }

    private async Task<LastSchema> GetLastSchema(string schemaName)
    {
        await CreateSchemaTableIfNotExists(schemaName);

        using (var cnx = new SqlConnection(DbMigrationConfig.ConnectionString))
        {
            var cmd = cnx.CreateCommand();
            cmd.CommandText = $"Select top 1 [Version], [Hash] from [{schemaName}] order by Version desc";

            await cnx.OpenAsync();
            var reader = await cmd.ExecuteReaderAsync();
            var result = new LastSchema();
            while (reader.Read())
            {
                result.Version = Convert.ToInt32(reader[0]);
                result.Hash = Convert.ToString(reader[1]);
            }
            await cnx.CloseAsync();

            return result;
        }
    }

    private async Task<int> ApplyScripts(LastSchema lastSchema, IEnumerable<SqlScript> patchList, string schemaName)
    {
        var newVersion = 0;
        foreach (var sqlScript in patchList.OrderBy(i => i.Version))
        {
            if (sqlScript.Version < DbMigrationConfig.StartAtVersion)
            {
                continue;
            }

            if (sqlScript.Version <= lastSchema.Version)
            {
                continue;
            }

            if (sqlScript.Version == lastSchema.Version
                && sqlScript.Hash == lastSchema.Hash)
            {
                continue;
            }

            using (var cnx = new SqlConnection(DbMigrationConfig.ConnectionString))
            {
                await cnx.OpenAsync();
                var ts = cnx.BeginTransaction(System.Data.IsolationLevel.Serializable, "SchemaUpdate");
                await ApplyPatch(cnx, ts, sqlScript);
                await UpgradeSchema(cnx, ts, sqlScript, schemaName);
                ts.Commit();
                await cnx.CloseAsync();
            }
            System.Diagnostics.Debug.WriteLine($"Schema {schemaName} apply script {sqlScript.Name}");
            newVersion = sqlScript.Version;
        }

        return newVersion;
    }

    private async Task ApplyPatch(SqlConnection cnx, SqlTransaction ts, SqlScript script)
    {
        var reader = new StringReader(script.Content);
        while (true)
        {
            var sql = ReadNextStatementFromStream(reader);
            if (sql == null)
            {
                break;
            }

            var cmd = cnx.CreateCommand();
            cmd.Transaction = ts;
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        reader.Close();
    }

    private string ReadNextStatementFromStream(StringReader reader)
    {
        var sb = new StringBuilder();

        string lineOfText;

        while (true)
        {
            lineOfText = reader.ReadLine();
            if (lineOfText == null)
            {

                if (sb.Length > 0)
                {
                    return sb.ToString();
                }
                else
                {
                    return null;
                }
            }

            if (lineOfText.TrimEnd().Equals("GO", StringComparison.InvariantCultureIgnoreCase))
            {
                break;
            }

            sb.Append(lineOfText + Environment.NewLine);
        }

        return sb.ToString();
    }

    private async Task UpgradeSchema(SqlConnection cnx, SqlTransaction ts, SqlScript script, string schemaName)
    {
        var query = $@"insert into [{schemaName}]
						(Version, Name, CreationDate, Script, Hash)
						values
						(@Version, @Name, @CreationDate, @Script, @Hash)";

        var cmd = cnx.CreateCommand();
        cmd.Transaction = ts;
        cmd.CommandType = System.Data.CommandType.Text;
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@Version", script.Version);
        cmd.Parameters.AddWithValue("@Name", script.Name);
        cmd.Parameters.AddWithValue("@CreationDate", DateTime.Now);
        cmd.Parameters.AddWithValue("@Script", script.Content);
        cmd.Parameters.AddWithValue("@Hash", script.Hash);
        await cmd.ExecuteNonQueryAsync();
    }



}
