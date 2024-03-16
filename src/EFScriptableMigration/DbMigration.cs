using System;
using System.Collections.Generic;

namespace EFScriptableMigration;

public class DbMigration
{
	public string ConnectionString { get; set; }
	public string SchemaName { get; set; }
	public int StartAtVersion { get; set; } = 1;
	public bool ApplyEmbededScripts { get; set; } = true;
	public Type EmbededTypeReference { get; set; }

	internal Dictionary<string, List<SqlScript>> ExtendedScripts { get; } = new();

	public void AddScript(string schemaName, SqlScript sqlScript)
	{
		sqlScript.Hash = sqlScript.Content.GetSHA256();
		if (!ExtendedScripts.ContainsKey(schemaName))
		{
			ExtendedScripts.Add(schemaName, new List<SqlScript>());
		}
		ExtendedScripts[schemaName].Add(sqlScript);
	}
}
