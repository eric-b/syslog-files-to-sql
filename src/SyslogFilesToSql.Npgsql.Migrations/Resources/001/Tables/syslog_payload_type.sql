CREATE TABLE syslog_payload_type(
	id smallserial PRIMARY KEY,
	name varchar(16) NOT NULL UNIQUE
);
