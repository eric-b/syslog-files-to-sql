CREATE TABLE syslog_facility (
	id smallserial PRIMARY KEY,
	name varchar(64) NOT NULL UNIQUE
);
