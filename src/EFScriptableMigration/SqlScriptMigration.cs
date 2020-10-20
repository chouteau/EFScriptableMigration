using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Linq;
using System.IO;

namespace EFScriptableMigration
{
	public class SqlScriptMigration
	{
		protected DbMigrationConfig DbMigrationConfig { get; set; }

		public void Run(DbMigrationConfig dbMigrationConfig)
		{
			if (dbMigrationConfig == null)
			{
				throw new NullReferenceException("config does not by null");
			}

			dbMigrationConfig.EnsureGoodConfig();

			DbMigrationConfig = dbMigrationConfig;
			CreateSchemaTable();
			IEnumerable<SqlPatch> scriptList = null;
			if (!string.IsNullOrEmpty(DbMigrationConfig.ScriptPath))
			{
				scriptList = GetScriptListFromFolder();
			}
			else
			{
				scriptList = GetScriptListFromEmbededResources();
			}
			if (scriptList.Count() == 0)
			{
				return;
			}
			var schemaId = GetCurrentSchemaId();
			var nexSchemaId = scriptList.Max(i => i.SchemaId);

			if (schemaId < nexSchemaId)
			{
				ApplyScripts(schemaId, scriptList);
			}
		}

		private void CreateSchemaTable()
		{
			using (var cnx = new SqlConnection(DbMigrationConfig.ConnectionString))
			{
				var cmd = cnx.CreateCommand();
				cmd.CommandText = @"
if not exists(select name from sysobjects where name = '@SchemaTableName' and xtype = 'U')
Begin
	Create table dbo.[@SchemaTableName] (
		Id int not null
		, CreationDate DateTime2 not null
		, Name varchar(100) not null
		, Script varchar(max) not null
	)
	alter table dbo.[@SchemaTableName] add constraint PK_@SchemaTableName_Id primary key (Id)
	Create unique index IX_@SchemaTableName_Id on [@SchemaTableName](Id)
End
".Replace("@SchemaTableName", DbMigrationConfig.SchemaName.Replace("'", ""));

				cnx.Open();
				cmd.ExecuteNonQuery();
				cnx.Close();
			}
		}

		private int GetCurrentSchemaId()
		{
			using (var cnx = new SqlConnection(DbMigrationConfig.ConnectionString))
			{
				var cmd = cnx.CreateCommand();
				cmd.CommandText = $"Select top 1 Id from [{DbMigrationConfig.SchemaName}] order by id desc";

				cnx.Open();
				var result = cmd.ExecuteScalar();
				cnx.Close();

				if (result == null)
				{
					return 0;
				}
				return (int)result;
			}
		}

		private IEnumerable<SqlPatch> GetScriptListFromEmbededResources()
		{
			var assembly = System.Reflection.Assembly.GetAssembly(this.GetType());
			var embededList = assembly.GetManifestResourceNames();
			var result = new List<SqlPatch>();

			foreach (var script in embededList)
			{
				if (script.IndexOf(DbMigrationConfig.EmbededScriptNamespace) == -1)
				{
					continue;
				}

				var scriptName = script.Replace(DbMigrationConfig.EmbededScriptNamespace + ".", "");
				var match = System.Text.RegularExpressions.Regex.Match(scriptName, @"^(?<version>\d+)-(?<name>[^\.]+).sql");
				if (!match.Success)
				{
					continue;
				}
				var version = match.Groups["version"].Value;
				var name = match.Groups["name"].Value;
				string content = null;
				using (var stream = assembly.GetManifestResourceStream(script))
				{
					if (stream == null)
					{
						continue;
					}
					var buffer = new byte[stream.Length];

					stream.Read(buffer, 0, buffer.Length);
					content = System.Text.Encoding.UTF8.GetString(buffer);
					content = content.Substring(1);
				}
				result.Add(new SqlPatch()
				{
					SchemaId = Convert.ToInt32(version),
					Name = name,
					Script = content,
				});
			}

			return result;

		}

		private IEnumerable<SqlPatch> GetScriptListFromFolder()
		{
			var list = from file in System.IO.Directory.GetFiles(DbMigrationConfig.ScriptPath, "*.sql")
					   orderby file
					   select file; 

			var result = new List<SqlPatch>();
			foreach (var file in list)
			{
				var fileName = System.IO.Path.GetFileName(file);
				fileName = fileName.Replace(".sql", "");
				var parts = fileName.Split('-');
				var sqlScript = new SqlPatch();
				sqlScript.SchemaId = Convert.ToInt32(parts[0]);
				sqlScript.Name = parts[1];
				sqlScript.Script = System.IO.File.ReadAllText(file);
				result.Add(sqlScript);
			}

			return result;
		}

		private int ApplyScripts(int startFrom, IEnumerable<SqlPatch> patchList)
		{
			int latestVersion = 0;
			foreach (var sqlScript in patchList.OrderBy(i => i.SchemaId))
			{
				if (sqlScript.SchemaId <= startFrom)
				{
					continue;
				}

				using (var cnx = new SqlConnection(DbMigrationConfig.ConnectionString))
				{
					cnx.Open();
					var ts = cnx.BeginTransaction(System.Data.IsolationLevel.Serializable, "SchemaUpdate");
					ApplyPatch(cnx, ts, sqlScript);
					UpgradeSchema(cnx, ts, sqlScript);
					ts.Commit();
					cnx.Close();
				}
				latestVersion = sqlScript.SchemaId;
			}

			return latestVersion;
		}

		private void ApplyPatch(SqlConnection cnx, SqlTransaction ts, SqlPatch script)
		{
			var reader = new StringReader(script.Script);
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
				cmd.ExecuteNonQuery();
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

				if (lineOfText.TrimEnd().ToUpper() == "GO")
				{
					break;
				}

				sb.Append(lineOfText + Environment.NewLine);
			}

			return sb.ToString();
		}

		private void UpgradeSchema(SqlConnection cnx, SqlTransaction ts, SqlPatch script)
		{
			var query = $@"insert into [{DbMigrationConfig.SchemaName}] 
						(Id, Name, CreationDate, Script) 
						values 
						(@Id, @Name, @CreationDate, @Script)";

			var cmd = cnx.CreateCommand();
			cmd.Transaction = ts;
			cmd.CommandType = System.Data.CommandType.Text;
			cmd.CommandText = query;
			cmd.Parameters.AddWithValue("@Id", script.SchemaId);
			cmd.Parameters.AddWithValue("@Name", script.Name);
			cmd.Parameters.AddWithValue("@CreationDate", DateTime.Now);
			cmd.Parameters.AddWithValue("@Script", script.Script);
			cmd.ExecuteNonQuery();
		}

	}
}
