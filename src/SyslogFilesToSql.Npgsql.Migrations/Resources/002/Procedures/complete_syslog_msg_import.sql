CREATE OR REPLACE PROCEDURE complete_syslog_msg_import(_max_days_to_keep smallint) 
    LANGUAGE plpgsql AS
$$
DECLARE
    _dates date[];
    _oldestDate date;
BEGIN

    _dates := ARRAY(SELECT DISTINCT created_on::date FROM public.syslog_msg_import);
    SELECT public._update_syslog_partitions(_max_days_to_keep, _dates) INTO _oldestDate;

    INSERT INTO public.syslog_app (name) 
    SELECT DISTINCT m.app 
    FROM public.syslog_msg_import m
    INNER JOIN public.syslog_file_imported f ON f.file_hash=m.file_hash
    WHERE m.app IS NOT NULL AND NOT f.is_complete AND m.created_on>=_oldestDate
    ON CONFLICT (name)
    DO NOTHING;

    INSERT INTO public.syslog_facility (name) 
    SELECT DISTINCT m.facility 
    FROM public.syslog_msg_import m
    INNER JOIN public.syslog_file_imported f ON f.file_hash=m.file_hash
    WHERE NOT f.is_complete AND m.created_on>=_oldestDate
    ON CONFLICT (name)
    DO NOTHING;
    
    INSERT INTO public.syslog_host (name) 
    SELECT DISTINCT m.host 
    FROM public.syslog_msg_import m
    INNER JOIN public.syslog_file_imported f ON f.file_hash=m.file_hash
    WHERE NOT f.is_complete AND m.created_on>=_oldestDate
    ON CONFLICT (name)
    DO NOTHING;

    INSERT INTO public.syslog_payload_type (name) 
    SELECT DISTINCT m.payload_type 
    FROM public.syslog_msg_import m
    INNER JOIN public.syslog_file_imported f ON f.file_hash=m.file_hash
    WHERE NOT f.is_complete AND m.created_on>=_oldestDate
    ON CONFLICT (name)
    DO NOTHING;

    INSERT INTO public.syslog_severity (name) 
    SELECT DISTINCT m.severity 
    FROM public.syslog_msg_import m
    INNER JOIN public.syslog_file_imported f ON f.file_hash=m.file_hash
    WHERE NOT f.is_complete AND m.created_on>=_oldestDate
    ON CONFLICT (name)
    DO NOTHING;

    ALTER TABLE syslog_msg DROP CONSTRAINT syslog_msg_id_app_fkey;
    ALTER TABLE syslog_msg DROP CONSTRAINT syslog_msg_id_file_fkey;
    ALTER TABLE syslog_msg DROP CONSTRAINT syslog_msg_id_facility_fkey;
    ALTER TABLE syslog_msg DROP CONSTRAINT syslog_msg_id_severity_fkey;
    ALTER TABLE syslog_msg DROP CONSTRAINT syslog_msg_id_host_fkey;
    ALTER TABLE syslog_msg DROP CONSTRAINT syslog_msg_id_payload_type_fkey;
    
    DROP INDEX IF EXISTS idx_syslog_msg_facility;
    DROP INDEX IF EXISTS idx_syslog_msg_severity;
    DROP INDEX IF EXISTS idx_syslog_msg_host;
    DROP INDEX IF EXISTS idx_syslog_msg_app;
    DROP INDEX IF EXISTS idx_syslog_msg_id;

    INSERT INTO public.syslog_msg 
    (
        id_file, 
        id_facility, 
        id_severity, 
        created_on, 
        id_host, 
        id_payload_type, 
        id_app, 
        pid, 
        msg_id, 
        msg
    ) 
    SELECT 
        fi.id,
        fa.id,
        se.id,
        m.created_on,
        ho.id,
        pa.id,
        ap.id,
        m.pid,
        m.msg_id,
        m.msg
    FROM public.syslog_msg_import m
    INNER JOIN public.syslog_file_imported fi ON fi.file_hash=m.file_hash
    INNER JOIN public.syslog_facility fa ON fa.name=m.facility
    INNER JOIN public.syslog_severity se ON se.name=m.severity
    INNER JOIN public.syslog_host ho ON ho.name=m.host
    INNER JOIN public.syslog_payload_type pa ON pa.name=m.payload_type
    LEFT OUTER JOIN public.syslog_app ap ON ap.name=m.app
    WHERE NOT fi.is_complete AND m.created_on>=_oldestDate;

    TRUNCATE TABLE public.syslog_msg_import;

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
    
    INSERT INTO public.syslog_msg_stat
    (id_host, id_app, date, msg_count)
    SELECT m.id_host, m.id_app, m.created_on::date, COUNT(*)
    FROM public.syslog_msg m
    INNER JOIN public.syslog_file_imported fi ON fi.id=m.id_file
    WHERE NOT fi.is_complete  AND m.id_app IS NOT NULL
    GROUP BY m.created_on::date, m.id_host, m.id_app
    ON CONFLICT (id_host, id_app, date)
    DO UPDATE SET msg_count=syslog_msg_stat.msg_count + excluded.msg_count;

    INSERT INTO public.syslog_msg_stat
    (id_host, date, msg_count)
    SELECT m.id_host, m.created_on::date, COUNT(*)
    FROM public.syslog_msg m
    INNER JOIN public.syslog_file_imported fi ON fi.id=m.id_file
    WHERE NOT fi.is_complete AND m.id_app IS NULL
    GROUP BY m.created_on::date, m.id_host
    ON CONFLICT (id_host, id_app, date)
    DO UPDATE SET msg_count=syslog_msg_stat.msg_count + excluded.msg_count;

    UPDATE public.syslog_file_imported fi
    SET is_complete=true, completed_on=CURRENT_TIMESTAMP
    WHERE NOT fi.is_complete AND EXISTS (SELECT 1 FROM public.syslog_msg m WHERE m.id_file=fi.id);

END;
$$;
