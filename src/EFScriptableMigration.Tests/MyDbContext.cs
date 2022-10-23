using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace EFScriptableMigration.Tests
{
	public class MyDbContext : DbContext
	{
		private readonly string _connectionString;

		public MyDbContext(string connectionString)
		{
			this._connectionString = connectionString;
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			optionsBuilder.EnableSensitiveDataLogging();
			optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
			optionsBuilder.UseSqlServer(_connectionString);
			base.OnConfiguring(optionsBuilder);
		}
		public DbSet<MyModel> MyModels { get; set; }
	}
}
