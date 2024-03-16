namespace EFScriptableMigration;

public sealed class SqlScript
{
	public int Version { get; set; }
	public string Name { get; set; }
	public string Content { get; set; }
	internal string Hash { get; set; }
}
