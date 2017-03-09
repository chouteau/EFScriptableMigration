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
			AppDomain.CurrentDomain.SetData(
				"DataDirectory",
				System.IO.Path.Combine(context.TestDeploymentDir, string.Empty));

			var csb = GetConnectionStringBuilder();

			if (System.IO.File.Exists(csb.AttachDBFilename))
			{
				return;
			}

			var initializer = new System.Data.Entity.CreateDatabaseIfNotExists<MyDbContext>();
			System.Data.Entity.Database.SetInitializer(initializer);

			using (var dbContext = new MyDbContext(csb.ConnectionString))
			{
				dbContext.Database.Initialize(true);
			}
		}

		[TestMethod]
		public void Create_Model_In_New_Database()
		{
			var initializer = new System.Data.Entity.CreateDatabaseIfNotExists<MyDbContext>();
			System.Data.Entity.Database.SetInitializer(initializer);
			using (var dbContext = new MyDbContext())
			{
				dbContext.Database.Initialize(true);
			}

			var migration = new EFScriptableMigration.ScriptableMigration<MyDbContext>("_myschema", "EFScriptableMigration.Tests.Scripts");
			System.Data.Entity.Database.SetInitializer<MyDbContext>(migration);

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
			var migration = new EFScriptableMigration.ScriptableMigration<MyDbContext>("_myschema", "EFScriptableMigration.Tests.Scripts");
			System.Data.Entity.Database.SetInitializer<MyDbContext>(migration);

			var model = new MyModel();
			var now = model.CreationDate = DateTime.Now;
			model.Name = "name";
			model.Ready = true;

			var csb = GetConnectionStringBuilder();

			var db = new MyDbContext(csb.ConnectionString);

			db.MyModels.Add(model);
			db.SaveChanges();

			var m = db.MyModels.OrderByDescending(i => i.Id).First();

			Check.That(m).IsNotNull();
			Check.That(m.CreationDate).IsEqualTo(now);
		}


		private static System.Data.SqlClient.SqlConnectionStringBuilder GetConnectionStringBuilder()
		{
			var cs = System.Configuration.ConfigurationManager.ConnectionStrings["TEST"].ConnectionString;
			var csBuilder = new System.Data.SqlClient.SqlConnectionStringBuilder(cs);
			var attachDbFileName = csBuilder.AttachDBFilename = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(MigrateDatabase).Assembly.Location), "TEST.mdf");

			return csBuilder;
		}
	}
}
