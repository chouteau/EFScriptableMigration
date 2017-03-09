using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.IO;

namespace EFScriptableMigration
{
	internal class MigrationService
	{
		private static Lazy<MigrationService> m_LazyMigrationService = new Lazy<MigrationService>(() =>
		{
			var result = new MigrationService();
			result.MigrationList = new System.Collections.Concurrent.ConcurrentBag<Migration>();
			result.Logger = new DiagnosticsLogger();
			return result;
		}, true);

		private MigrationService() {}

		internal System.Collections.Concurrent.ConcurrentBag<Migration> MigrationList { get; private set; }
		protected ILogger Logger { get; set; }

		public static MigrationService Current
		{
			get
			{
				return m_LazyMigrationService.Value;
			}
		}

		public void Add<TContext>(string schemaTableName, string nameSpace)
			where TContext : DbContext
		{
			var migration = MigrationList.SingleOrDefault(i => i.DbContextType == typeof(TContext));
			if (migration != null)
			{
				return;
			}

			migration = new Migration();
			migration.DbContextType = typeof(TContext);
			migration.SchemaTableName = schemaTableName;
			migration.EmbededScriptNamespace = nameSpace;

			MigrationList.Add(migration);
		}

		public void Process<TContext>(TContext context)
			where TContext : DbContext
		{
			if (context == null)
			{
				Logger.Warn($"context is null");
				return;
			}

			var migration = MigrationList.FirstOrDefault(i => i.DbContextType == typeof(TContext));
			if (migration == null)
			{
				Logger.Warn($"migration not found for type {typeof(TContext).AssemblyQualifiedName}");
				return;
			}

			if (migration.IsProcessed)
			{
				Logger.Warn($"migration already processed for type {typeof(TContext).AssemblyQualifiedName}");
				return;
			}

			Logger.Info($"Start migration for type {typeof(TContext).AssemblyQualifiedName} with schema {migration.SchemaTableName}");

			migration.ObjectContext = ((IObjectContextAdapter)context).ObjectContext;
			migration.CreateSchemaTableIfNotExist();

			var currentDbSchemaId = migration.GetSchemaId();
			Logger.Info($"Current schema is {currentDbSchemaId} {migration.SchemaTableName}");

			var patchList = migration.GetSqlPatchList<TContext>();
			if (patchList.Count == 0)
			{
				Logger.Info($"No patch found");
				return;
			}
			var nextSchemaId = patchList.Max(i => i.SchemaId);
			if (currentDbSchemaId == nextSchemaId)
			{
				Logger.Info($"No update found");
				return;
			}

			var latestVersion = migration.ApplyPatchs(currentDbSchemaId + 1, patchList);
			Logger.Info($"Patch success with version {latestVersion}");

			migration.IsProcessed = true;
		}
	}
}
