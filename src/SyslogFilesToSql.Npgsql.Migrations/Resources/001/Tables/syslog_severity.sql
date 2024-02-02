CREATE TABLE syslog_severity(
	id smallserial PRIMARY KEY,
	name varchar(64) NOT NULL UNIQUE
);
