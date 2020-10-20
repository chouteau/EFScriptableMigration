using System;
using System.Collections.Generic;
using System.Text;

namespace EFScriptableMigration
{
	public class DbMigrationConfig
	{
		public string ConnectionString { get; set; }
		public string SchemaName { get; set; }
		public string ScriptPath { get; set; }
		public string EmbededScriptNamespace { get; set; }

		public void EnsureGoodConfig()
		{
			if (string.IsNullOrEmpty(ConnectionString))
			{
				throw new NullReferenceException("connectionString does not by null or empty");
			}
			if (string.IsNullOrEmpty(SchemaName))
			{
				throw new NullReferenceException("schemaName does not by null or empty");
			}
			if (string.IsNullOrEmpty(ScriptPath)
				&& string.IsNullOrEmpty(EmbededScriptNamespace))
			{
				throw new NullReferenceException("ScriptPath or EmbededScriptNamespace does not by null or empty");
			}
		}

		internal System.Data.SqlClient.SqlConnectionStringBuilder ConnectionStringBuilder
		{
			get
			{
				if (ConnectionString == null)
				{
					return null;
				}
				return new System.Data.SqlClient.SqlConnectionStringBuilder(ConnectionString);
			}
		}
	}
}
