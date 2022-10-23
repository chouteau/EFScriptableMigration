using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EFScriptableMigration;

internal sealed class LastSchema
{
    public int Version { get; set; }
    public string Hash { get; set; }
}
