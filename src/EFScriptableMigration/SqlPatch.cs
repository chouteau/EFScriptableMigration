using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration
{
	internal class SqlPatch
	{
		public int SchemaId { get; set; }
		public string Name { get; set; }
		public string Script { get; set; }
	}
}
