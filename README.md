# Entity Framework Scriptable Migration

## About

**EFScriptableMigration** allows to migrate sql schema via sql scripts that are embedded in assemblies and written manually.

## Where can I get it ?

First, [install Nuget](http://docs.nuget.org/docs/start-here/installing-nuget) then, install [EFScriptableMigration](https://www.nuget.org/packages/EFScriptableMigration/) from the package manager console.

> PM> Install-Package EFScriptableMigration

## Usages :

Set initializer for any DbContext with scriptable migration like this :
```c#
var migration = new EFScriptableMigration.ScriptableMigration<MyDbContext>("_myschema");
System.Data.Entity.Database.SetInitializer<MyDbContext>(migration);
```

In assembly contains DbContext create folder named /Scripts

Add sql script file as **embedded resource** with name "**001-scriptname.sql**" like this :
```sql
if exists(select * from sysobjects where name = 'MyModel' and xtype = 'U')
Begin
	drop table dbl.MyModel
End
go

Create Table dbo.MyModel (
	Id int identity(1,1) not null
	, Name varchar(100) not null
	, CreationDate datetime not null
	, Ready bit not null
)
go

alter table dbo.MyModel add constraint PK_MyModel_Id primary key (Id)
Go
```

Create your model
```c#
public class MyModel
{
	public int Id { get; set; }
	public string Name { get; set; }
	public DateTime CreationDate { get; set; }
	public bool Ready { get; set; }
}
```

Create your DbContext
```c#
public class MyDbContext : DbContext
{
	public MyDbContext()
		: base("name=TEST")
	{

	}

	public IDbSet<MyModel> MyModels { get; set; }

	protected override void OnModelCreating(DbModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);
		modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
	}
}
```
