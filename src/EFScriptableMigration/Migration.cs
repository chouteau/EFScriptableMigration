using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Data.SqlClient;

namespace EFScriptableMigration
{
	internal class Migration
	{
		public Migration()
		{
			IsProcessed = false;
			this.Logger = new DiagnosticsLogger();
		}

		public string SchemaTableName { get; set; }
		public string EmbededScriptNamespace { get; set; }
		public Type DbContextType { get; set; }
		public bool IsProcessed { get; set; }
		protected ILogger Logger { get; set; }
		public ObjectContext ObjectContext { get; set; }

		public void CreateSchemaTableIfNotExist()
		{
			if (SchemaTableName.IndexOf("'") != -1)
			{
				throw new Exception("SchemaTableName with quote symbol was denied");
			}
			var createScript = @"
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
";
			createScript = createScript.Replace("@SchemaTableName", SchemaTableName);
			ObjectContext.ExecuteStoreCommand(createScript);
		}

		public int GetSchemaId()
		{
			var query = $"select top 1 id from [{SchemaTableName}] order by id desc";
			var lastSchema = ObjectContext.ExecuteStoreQuery<int>(query);

			return lastSchema.FirstOrDefault();
		}

		public int ApplyPatchs(int startFrom, List<SqlPatch> patchList)
		{
			int latestVersion = 0;
			foreach (var sqlPatch in patchList.OrderBy(i => i.SchemaId))
			{
				if (sqlPatch.SchemaId < startFrom)
				{
					continue;
				}

				Logger.Info($"try to patch schema with version {sqlPatch.SchemaId}");

				ObjectContext.CommandTimeout = 600;
				using (var ts = GetNewReadCommittedTransaction())
				{
					ApplyPatch(sqlPatch);
					RefreshAllViews();
					UpgradeSchema(sqlPatch);
					ts.Complete();
				}
				latestVersion = sqlPatch.SchemaId;
			}

			return latestVersion;
		}

		public void ApplyPatch(SqlPatch patch)
		{
			var reader = new StringReader(patch.Script);
			while (true)
			{
				var sql = ReadNextStatementFromStream(reader);
				if (sql == null)
				{
					break;
				}
				try
				{
					ObjectContext.ExecuteStoreCommand(sql);
				}
				catch (Exception ex)
				{
					var message = ex.Message + " " + patch.Name + " " + sql;
					var crashEx = new Exception(message, ex);
					crashEx.Data.Add("RepositoryInitializer.ApplyPatch." + patch.Name, sql);
					Logger.Error($"sql patch fail with script :\n {sql}");
					throw crashEx;
				}
			}

			reader.Close();
		}

		public List<SqlPatch> GetSqlPatchList<TContext>()
		{
			var result = new List<SqlPatch>();
			var assembly = System.Reflection.Assembly.GetAssembly(typeof(TContext));
			var embededList = assembly.GetManifestResourceNames();
			foreach (var script in embededList)
			{
				if (script.IndexOf(EmbededScriptNamespace) == -1)
				{
					continue;
				}

				var scriptName = script.Replace(EmbededScriptNamespace + ".", "");
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

		private void RefreshAllViews()
		{
			string sql =
				@"
select 
name as vname
into #temp
from sysobjects 
where 
	xtype = 'V'

declare my_cursor Cursor
for 
select vname from #temp
where vname <> 'database_firewall_rules'

open my_cursor
declare @Name varchar(1024)
fetch next from my_cursor into @Name
while (@@Fetch_status <> -1)
begin
	exec sp_refreshview @ViewName = @Name
	fetch next from my_cursor into @Name
end
close my_cursor
deallocate my_cursor

drop table #temp
";
			ObjectContext.ExecuteStoreCommand(sql);
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

		private void UpgradeSchema(SqlPatch patch)
		{
			var query = $@"insert into [{SchemaTableName}] 
						(Id, Name, CreationDate, Script) 
						values 
						(@Id, @Name, @CreationDate, @Script)";

			ObjectContext.ExecuteStoreCommand(query,
				new SqlParameter("@Id", patch.SchemaId),
				new SqlParameter("@Name", patch.Name),
				new SqlParameter("@CreationDate", DateTime.Now),
				new SqlParameter("@Script", patch.Script)
			);
		}

		private System.Transactions.TransactionScope GetNewReadCommittedTransaction()
		{
			return new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.RequiresNew
							, new System.Transactions.TransactionOptions()
							{
								IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted
							});
		}

	}
}
