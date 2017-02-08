using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration
{
	internal class SchemaDbContext : DbContext
	{
		public SchemaDbContext(string nameOrConnectionString, string schemaTable)
			: base(nameOrConnectionString)
		{
			this.SchemaTableName = schemaTable;
		}

		protected string SchemaTableName { get; set; }

		public IDbSet<Schema> Schema { get; set; }

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Configurations.Add(new SchemaMap());
			modelBuilder.Entity<Schema>().ToTable(SchemaTableName);
		}
	}
}
