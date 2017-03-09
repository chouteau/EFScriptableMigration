

if not exists(select * from sysobjects where name = 'MyModel' and xtype = 'U')
Begin

	Create Table dbo.MyModel (
		Id int identity(1,1) not null
		, Name varchar(100) not null
		, CreationDate datetime not null
		, Ready bit not null
	)

	alter table dbo.MyModel add constraint PK_MyModel_Id primary key (Id)

End
Go