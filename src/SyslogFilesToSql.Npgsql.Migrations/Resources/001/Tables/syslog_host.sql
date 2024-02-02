CREATE TABLE syslog_host(
	id smallserial PRIMARY KEY,
	name varchar(255) NOT NULL UNIQUE
);
