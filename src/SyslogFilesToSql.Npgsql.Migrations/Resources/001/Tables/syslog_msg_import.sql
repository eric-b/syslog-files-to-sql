CREATE UNLOGGED TABLE syslog_msg_import (
	file_hash bytea NOT NULL,
	facility varchar(64) NOT NULL,
	severity varchar(64) NOT NULL,
	created_on timestamp without time zone NOT NULL,
	host varchar(255) NOT NULL,
	payload_type varchar(16) NOT NULL,
	app varchar(255) NULL,
	pid int NULL,
	msg_id int NULL,
	msg varchar(8096) NOT NULL
);
