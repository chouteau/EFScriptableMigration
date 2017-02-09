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
		}

		[TestMethod]
		public void CRUD_MyModel()
		{
			var migration = new EFScriptableMigration.ScriptableMigration<MyDbContext>("_myschema");
			System.Data.Entity.Database.SetInitializer<MyDbContext>(migration);

			var model = new MyModel();
			model.CreationDate = DateTime.Now;
			model.Name = "name";
			model.Ready = true;

			var db = new MyDbContext();

			db.MyModels.Add(model);
			db.SaveChanges();

			var m = db.MyModels.First();

			Check.That(m).IsNotNull();
		}
	}
}
