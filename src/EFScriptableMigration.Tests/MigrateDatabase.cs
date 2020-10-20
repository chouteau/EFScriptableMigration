using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using NFluent;

namespace EFScriptableMigration.Tests
{
	[TestClass]
	public class MigrateDatabase
	{
		[ClassInitialize]
		public static void ClassInitialize(TestContext context)
		{
			var initializer = new System.Data.Entity.CreateDatabaseIfNotExists<MyDbContext>();
			System.Data.Entity.Database.SetInitializer(initializer);
		}

		[TestMethod]
		public void Create_Model_In_New_Database()
		{
			var migrationConfig = new DbMigrationConfig();
			migrationConfig.SchemaName = "_myschema";
			migrationConfig.EmbededScriptNamespace = "EFScriptableMigration.Tests.Scripts";
			migrationConfig.ConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"].ConnectionString;
			var migration = new SqlScriptMigration();
			migration.Run(migrationConfig);

			var model = new MyModel();
			model.CreationDate = DateTime.Now;
			model.Name = "name";
			model.Ready = true;

			var db = new MyDbContext();

			db.MyModels.Add(model);
			db.SaveChanges();

			var m = db.MyModels.OrderByDescending(i => i.Id).First();

			Check.That(m).IsNotNull();
		}

		[TestMethod]
		public void Create_Model_In_Test_Database()
		{
			var migrationConfig = new DbMigrationConfig();
			migrationConfig.SchemaName = "_myschema";
			migrationConfig.EmbededScriptNamespace = "EFScriptableMigration.Tests.Scripts";
			migrationConfig.ConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["Test"].ConnectionString;

			var migration = new EFScriptableMigration.ScriptableMigration<MyDbContext>(migrationConfig);
			System.Data.Entity.Database.SetInitializer<MyDbContext>(migration);

			var model = new MyModel();
			var now = model.CreationDate = DateTime.Now;
			model.Name = "name";
			model.Ready = true;

			var db = new MyDbContext(migrationConfig.ConnectionString);

			db.MyModels.Add(model);
			db.SaveChanges();

			var m = db.MyModels.OrderByDescending(i => i.Id).First();

			Check.That(m).IsNotNull();
			Check.That(m.CreationDate).IsEqualTo(now);
		}
	}
}
