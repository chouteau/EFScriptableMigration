using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration
{
	public class ScriptableMigration<TContext> : IDatabaseInitializer<TContext>
		where TContext : DbContext
	{
		public ScriptableMigration(string schemaTableName, string embededScriptNameSpace)
		{
			MigrationService.Current.Add<TContext>(schemaTableName, embededScriptNameSpace);
		}

		public void InitializeDatabase(TContext context)
		{
			Database.SetInitializer<TContext>(null);
			MigrationService.Current.Process(context);
		}
	}
}
