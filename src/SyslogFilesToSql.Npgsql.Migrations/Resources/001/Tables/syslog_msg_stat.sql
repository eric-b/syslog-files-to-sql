CREATE TABLE syslog_msg_stat (
	id serial PRIMARY KEY,
	id_host int NOT NULL REFERENCES syslog_host(id),
	id_app smallint NULL REFERENCES syslog_app(id),
	date date NOT NULL,
	msg_count int NOT NULL
);

CREATE UNIQUE INDEX idx_syslog_msg_stat_unique ON syslog_msg_stat (date, id_host, id_app) NULLS NOT DISTINCT;