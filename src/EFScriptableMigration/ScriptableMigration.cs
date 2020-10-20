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
		protected DbMigrationConfig DbMigrationConfig { get; set; }
		public ScriptableMigration(DbMigrationConfig config)
		{
			DbMigrationConfig = config;
		}

		public void InitializeDatabase(TContext context)
		{
			Database.SetInitializer<TContext>(null);
			var sqlScriptMigration = new SqlScriptMigration();
			sqlScriptMigration.Run(DbMigrationConfig);
		}
	}
}
