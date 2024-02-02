CREATE TABLE syslog_file_imported (
	id serial PRIMARY KEY,
	file_hash bytea UNIQUE NOT NULL,
	is_complete boolean NOT NULL DEFAULT false,
	inserted_on timestamp with time zone NOT NULL DEFAULT current_timestamp,
	completed_on timestamp with time zone,
	file_path varchar(1024) NOT NULL
);
