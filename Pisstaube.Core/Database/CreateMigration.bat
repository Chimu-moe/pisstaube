@echo off

set MARIADB_HOST=127.0.0.1
set MARIADB_PORT=3306
set MARIADB_DATABASE=pisstaube
set MARIADB_USERNAME=root
set MARIADB_PASSWORD=12341

dotnet ef migrations add %1
