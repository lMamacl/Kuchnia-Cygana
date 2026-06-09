:ON ERROR EXIT

DECLARE @dbName sysname = N'$(DbName)';
DECLARE @adminPassword nvarchar(256) = N'$(AdminPassword)';
DECLARE @pracownikPassword nvarchar(256) = N'$(PracownikPassword)';
DECLARE @klientPassword nvarchar(256) = N'$(KlientPassword)';

IF NULLIF(@dbName, N'') IS NULL
    THROW 51000, 'DbName sqlcmd variable is required.', 1;

IF NULLIF(@adminPassword, N'') IS NULL
    THROW 51001, 'AdminPassword sqlcmd variable is required.', 1;

IF NULLIF(@pracownikPassword, N'') IS NULL
    THROW 51002, 'PracownikPassword sqlcmd variable is required.', 1;

IF NULLIF(@klientPassword, N'') IS NULL
    THROW 51003, 'KlientPassword sqlcmd variable is required.', 1;

IF DB_ID(@dbName) IS NULL
BEGIN
    DECLARE @createDatabaseSql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@dbName) + N';';
    EXEC(@createDatabaseSql);
END;

DECLARE @adminPasswordSql nvarchar(512) = REPLACE(@adminPassword, N'''', N'''''');
DECLARE @pracownikPasswordSql nvarchar(512) = REPLACE(@pracownikPassword, N'''', N'''''');
DECLARE @klientPasswordSql nvarchar(512) = REPLACE(@klientPassword, N'''', N'''''');
DECLARE @loginSql nvarchar(max);

IF SUSER_ID(N'admin') IS NULL
    SET @loginSql = N'CREATE LOGIN [admin] WITH PASSWORD = N''' + @adminPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@dbName) + N';';
ELSE
    SET @loginSql = N'ALTER LOGIN [admin] WITH PASSWORD = N''' + @adminPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@dbName) + N';';
EXEC(@loginSql);

IF SUSER_ID(N'pracownik') IS NULL
    SET @loginSql = N'CREATE LOGIN [pracownik] WITH PASSWORD = N''' + @pracownikPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@dbName) + N';';
ELSE
    SET @loginSql = N'ALTER LOGIN [pracownik] WITH PASSWORD = N''' + @pracownikPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@dbName) + N';';
EXEC(@loginSql);

IF SUSER_ID(N'klient') IS NULL
    SET @loginSql = N'CREATE LOGIN [klient] WITH PASSWORD = N''' + @klientPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@dbName) + N';';
ELSE
    SET @loginSql = N'ALTER LOGIN [klient] WITH PASSWORD = N''' + @klientPasswordSql + N''', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = ' + QUOTENAME(@dbName) + N';';
EXEC(@loginSql);

DECLARE @grantDatabaseAccessSql nvarchar(max) = N'
USE ' + QUOTENAME(@dbName) + N';

IF USER_ID(N''admin'') IS NULL
    CREATE USER [admin] FOR LOGIN [admin];
ELSE
    ALTER USER [admin] WITH LOGIN = [admin];

IF USER_ID(N''pracownik'') IS NULL
    CREATE USER [pracownik] FOR LOGIN [pracownik];
ELSE
    ALTER USER [pracownik] WITH LOGIN = [pracownik];

IF USER_ID(N''klient'') IS NULL
    CREATE USER [klient] FOR LOGIN [klient];
ELSE
    ALTER USER [klient] WITH LOGIN = [klient];

IF ISNULL(IS_ROLEMEMBER(N''db_owner'', N''admin''), 0) <> 1
    ALTER ROLE [db_owner] ADD MEMBER [admin];

IF ISNULL(IS_ROLEMEMBER(N''db_owner'', N''pracownik''), 0) = 1
    ALTER ROLE [db_owner] DROP MEMBER [pracownik];

IF ISNULL(IS_ROLEMEMBER(N''db_owner'', N''klient''), 0) = 1
    ALTER ROLE [db_owner] DROP MEMBER [klient];

IF ISNULL(IS_ROLEMEMBER(N''db_datareader'', N''pracownik''), 0) <> 1
    ALTER ROLE [db_datareader] ADD MEMBER [pracownik];

IF ISNULL(IS_ROLEMEMBER(N''db_datawriter'', N''pracownik''), 0) <> 1
    ALTER ROLE [db_datawriter] ADD MEMBER [pracownik];

IF ISNULL(IS_ROLEMEMBER(N''db_datareader'', N''klient''), 0) <> 1
    ALTER ROLE [db_datareader] ADD MEMBER [klient];

IF ISNULL(IS_ROLEMEMBER(N''db_datawriter'', N''klient''), 0) <> 1
    ALTER ROLE [db_datawriter] ADD MEMBER [klient];
';

EXEC(@grantDatabaseAccessSql);
