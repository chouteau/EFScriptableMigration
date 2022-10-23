using System;
using System.Configuration;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using NFluent;

namespace EFScriptableMigration.Tests
{
	[TestClass]
	public class MigrationTests
	{
		private static IConfiguration _configuration;
		private static string _currentDirectory;
		private static string _connectionString;

		[ClassInitialize]
		public static void ClassInitialize(TestContext context)
		{
			var configurationBuilder = new ConfigurationBuilder();
			configurationBuilder.AddJsonFile("appsettings.json");
            _currentDirectory = System.IO.Path.GetDirectoryName(typeof(MigrationTests).Assembly.Location);
			configurationBuilder.SetBasePath(_currentDirectory);
            _configuration = configurationBuilder.Build();

   //         var existingDbFileName = System.IO.Path.Combine(_currentDirectory, "Test.mdf");
			//if (System.IO.File.Exists(existingDbFileName))
			//{
			//	System.IO.File.Delete(existingDbFileName);
			//}
   //         existingDbFileName = System.IO.Path.Combine(_currentDirectory, "Test_log.ldf");
   //         if (System.IO.File.Exists(existingDbFileName))
   //         {
   //             System.IO.File.Delete(existingDbFileName);
   //         }

			_connectionString = _configuration.GetConnectionString("Test");
        }

        [TestMethod]
        public async Task BasicMigration()
        {
			await CreateDatabase();

            var migration = new DbMigration()
            {
                ConnectionString = _connectionString,
                SchemaName = "All",
                StartAtVersion = 0,
                EmbededTypeReference = this.GetType()
            };
            var report = await migration.Start();

            Check.That(report.AppliedScriptList.Any()).IsTrue();

            report = await migration.Start();

            Check.That(report.AppliedScriptList.Any()).IsFalse();
        }

        [TestMethod]
        public async Task Migration_With_Same_Shema_Version()
        {
            await CreateDatabase();

            var migration = new DbMigration()
            {
                ConnectionString = _connectionString,
                SchemaName = "All",
                StartAtVersion = 0,
                EmbededTypeReference = this.GetType()
            };

            var schema = "__schema_myschemav2";
            migration.AddScript(schema, new SqlScript()
            {
                Version = 2,
                Content = "Select GetDate()",
                Name = "Select Date",
            });

            migration.AddScript(schema, new SqlScript()
            {
                Version = 2,
                Content = "Select NewId()",
                Name = "Select NewId",
            });

            var report = await migration.Start();

            Check.That(report.AppliedScriptList.Any()).IsTrue();
            Check.That(report.AppliedScriptList.Count).IsEqualTo(2);

        }

        [TestMethod]
		public async Task Create_Model_In_New_Database()
		{
			await CreateDatabase();

			var migration = new DbMigration()
			{
				ConnectionString = _connectionString,
				SchemaName = "MySchema1",
				EmbededTypeReference = this.GetType()
			};
			var report = await migration.Start();
            Check.That(report.AppliedScriptList.Any()).IsTrue();

            var model = new MyModel();
			model.CreationDate = DateTime.Now;
			model.Name = "name";
			model.Ready = true;
			model.NewProperty = "newproperty";

			var db = new MyDbContext(_connectionString);

			db.MyModels.Add(model);
			var changeCount = await db.SaveChangesAsync();
			Check.That(changeCount).IsStrictlyGreaterThan(0);

			var m = db.MyModels.OrderByDescending(i => i.Id).First();

			Check.That(model.NewProperty).IsEqualTo("newproperty");

			Check.That(m).IsNotNull();
		}

		[TestMethod]
		public async Task Create_Model_In_Test_Database()
		{
			var migration = new DbMigration()
			{
				ConnectionString = _connectionString,
				SchemaName = "MySchema1",
				EmbededTypeReference = this.GetType()
            };
            var report = await migration.Start();

            var model = new MyModel();
			var now = model.CreationDate = DateTime.Now;
			model.Name = "name";
			model.Ready = true;

			var db = new MyDbContext(migration.ConnectionString);

			db.MyModels.Add(model);
			db.SaveChanges();

			var m = db.MyModels.OrderByDescending(i => i.Id).First();

			Check.That(m).IsNotNull();
			Check.That(m.CreationDate).IsEqualTo(now);
		}

		private async Task CreateDatabase()
		{
            var db = new MyDbContext(_connectionString);
			await db.Database.EnsureDeletedAsync();
			await db.Database.EnsureCreatedAsync();
        }
    }
}
