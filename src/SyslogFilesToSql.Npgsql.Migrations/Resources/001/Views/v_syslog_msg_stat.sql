CREATE VIEW public.v_syslog_msg_stat AS
SELECT
	s.id, 
	ho.name as host, 
	ap.name as app, 
	s.date,
	s.msg_count
FROM public.syslog_msg_stat s
INNER JOIN public.syslog_host ho on ho.id=s.id_host
LEFT OUTER JOIN public.syslog_app ap on ap.id=s.id_app;