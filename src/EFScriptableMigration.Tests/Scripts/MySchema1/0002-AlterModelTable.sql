

if COL_LENGTH('MyModel', 'NewProperty') is null
Begin
	alter table MyModel add NewProperty varchar(50) null
End
Go
