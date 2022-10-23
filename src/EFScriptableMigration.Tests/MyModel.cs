using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration.Tests
{
	[Table("MyModel")]

    public class MyModel
	{
		[Key]
		public int Id { get; set; }
		public string Name { get; set; }
		public DateTime CreationDate { get; set; }
		public bool Ready { get; set; }
		public string NewProperty { get; set; }
	}
}
