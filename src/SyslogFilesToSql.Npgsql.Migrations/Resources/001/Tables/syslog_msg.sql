CREATE TABLE syslog_msg (
	id bigint GENERATED ALWAYS AS IDENTITY,
	id_file int NOT NULL,
	id_facility smallint NOT NULL,
	id_severity smallint NOT NULL,
	created_on timestamp without time zone NOT NULL,
	id_host smallint NOT NULL,
	id_payload_type smallint NOT NULL,
	id_app smallint NULL,
	pid int NULL,
	msg_id int NULL,
	msg varchar(8096) NOT NULL
) PARTITION BY LIST ((created_on::date));

ALTER TABLE syslog_msg ADD CONSTRAINT syslog_msg_id_app_fkey FOREIGN KEY (id_app) REFERENCES syslog_app(id);
ALTER TABLE syslog_msg ADD CONSTRAINT syslog_msg_id_file_fkey FOREIGN KEY (id_file) REFERENCES syslog_file_imported(id);
ALTER TABLE syslog_msg ADD CONSTRAINT syslog_msg_id_facility_fkey FOREIGN KEY (id_facility) REFERENCES syslog_facility(id);
ALTER TABLE syslog_msg ADD CONSTRAINT syslog_msg_id_severity_fkey FOREIGN KEY (id_severity) REFERENCES syslog_severity(id);
ALTER TABLE syslog_msg ADD CONSTRAINT syslog_msg_id_host_fkey FOREIGN KEY (id_host) REFERENCES syslog_host(id);
ALTER TABLE syslog_msg ADD CONSTRAINT syslog_msg_id_payload_type_fkey FOREIGN KEY (id_payload_type) REFERENCES syslog_payload_type(id);

CREATE INDEX idx_syslog_msg_facility ON syslog_msg (id_facility);
CREATE INDEX idx_syslog_msg_severity ON syslog_msg (id_severity);
CREATE INDEX idx_syslog_msg_host ON syslog_msg (id_host);
CREATE INDEX idx_syslog_msg_app ON syslog_msg (id_app);
CREATE INDEX idx_syslog_msg_id ON syslog_msg (msg_id);