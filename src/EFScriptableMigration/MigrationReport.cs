using System.Collections.Generic;

namespace EFScriptableMigration;

public class MigrationReport
{
	public Dictionary<string, int> LastSchema { get; set; } = new();
	public Dictionary<string, int> AppliedScriptList { get; set; } = new();
}
