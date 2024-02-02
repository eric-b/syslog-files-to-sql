CREATE OR REPLACE FUNCTION _update_syslog_partitions(_max_partitions smallint, _dates date[]) 
RETURNS date
    LANGUAGE plpgsql AS
$$
DECLARE
    _dateStr varchar;
    _tableName varchar;
    _date date;
    _partitions varchar[];
    i int;
BEGIN

    FOREACH _date IN ARRAY _dates
    LOOP
        _dateStr := to_char(_date, 'YYYYMMDD');
        _tableName := 'syslog_msg_' || _dateStr;
        IF (NOT EXISTS (SELECT FROM pg_tables WHERE  schemaname = 'public' AND    tablename  = _tableName)) THEN

            EXECUTE format('
                CREATE TABLE %s (LIKE syslog_msg INCLUDING INDEXES);
                ALTER TABLE syslog_msg ATTACH PARTITION %s FOR VALUES IN (%L);', _tableName, _tableName, _dateStr);

        END IF;
    END LOOP;

    _partitions := ARRAY(
        SELECT child.relname 
        FROM pg_inherits
            JOIN pg_class parent            ON pg_inherits.inhparent = parent.oid
            JOIN pg_class child             ON pg_inherits.inhrelid   = child.oid
            JOIN pg_namespace nmsp_parent   ON nmsp_parent.oid  = parent.relnamespace
            JOIN pg_namespace nmsp_child    ON nmsp_child.oid   = child.relnamespace
        WHERE parent.relname='syslog_msg'
        ORDER BY child.relname DESC);


    FOR i IN (_max_partitions+1)..ARRAY_LENGTH(_partitions, 1) LOOP
        _tableName := _partitions[i];
        EXECUTE format('
            ALTER TABLE syslog_msg DETACH partition %s;
            DROP TABLE %s;', _tableName, _tableName);
    END LOOP;

    -- returns oldest partition date
    IF ARRAY_LENGTH(_partitions, 1) >= _max_partitions THEN
        _tableName := _partitions[_max_partitions];
    ELSE
        _tableName := _partitions[ARRAY_LENGTH(_partitions, 1)];
    END IF;

    _dateStr := substring(_tableName from 12 for 8);
    _date := to_date(_dateStr, 'YYYYMMDD');
    RETURN _date;
    
END;
$$;
