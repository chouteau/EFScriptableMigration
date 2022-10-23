using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration;

public class MigrationReport
{
    public Dictionary<string, int> LastSchema { get; set; } = new();
    public Dictionary<string, int> AppliedScriptList { get; set; } = new();
}
