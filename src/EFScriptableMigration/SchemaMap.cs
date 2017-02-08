using System;
using System.Collections.Generic;
using System.Data.Entity.ModelConfiguration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration
{
	internal class SchemaMap : EntityTypeConfiguration<Schema>
	{
		public SchemaMap()
			: base()
		{
			this.HasKey(e => e.Id);

			this.Property(e => e.Id)
				.IsRequired()
				.HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None);

			this.Property(e => e.Name)
				.IsRequired()
				.HasMaxLength(100);

			this.Property(e => e.Script)
				.IsMaxLength();
		}
	}
}
