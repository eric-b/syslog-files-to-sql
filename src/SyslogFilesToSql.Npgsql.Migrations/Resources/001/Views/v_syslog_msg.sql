CREATE VIEW public.v_syslog_msg AS
SELECT
	m.created_on, 
	fa.name as facility, 
	se.name as severity, 
	ho.name as host, 
	ap.name as app, 
	m.pid, 
	m.msg_id, 
	m.msg, 
	pa.name as payload_type
FROM public.syslog_msg m
INNER JOIN public.syslog_facility fa on fa.id=m.id_facility
INNER JOIN public.syslog_severity se on se.id=m.id_severity
INNER JOIN public.syslog_host ho on ho.id=m.id_host
INNER JOIN public.syslog_payload_type pa on pa.id=m.id_payload_type
LEFT OUTER JOIN public.syslog_app ap on ap.id=m.id_app;