using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration.Tests
{
	public class MyModel
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public DateTime CreationDate { get; set; }
		public bool Ready { get; set; }
		public string NewProperty { get; set; }
	}
}
