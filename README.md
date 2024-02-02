# SyslogFilesToSql

## Why SyslogFilesToSql ?

This tool is tailored to my needs: 

I have multiple devices in my local network generating syslog messages. They all send to a remote syslog server, which is hosted on my QNAP NAS (still in my local network). Logs are stored in files with rollover when file size reaches 10 MB and are named after rollover `syslog_YYYY_MM_dd[.#]` (examples: syslog_2024_01_20, syslog_2024_01_20.1, syslog_2024_01_20.2, ...).

I also have a PostgreSQL database running in a Docker container. I aim to forward a subset of syslog messages to this database, which makes troubleshooting and processing of these messages easier.

I run this tool inside a mini Kubernetes cluster (like K3S or MicroK8S, I tested both). Syslog files are read through an NFS volume (pointing to syslog files stored in the NAS) mounted in the pod running the tool.

Syslog daemons are not all identical in their message formatting. I use nuget package [SyslogDecode](https://github.com/microsoft/SyslogDecode) from Microsoft, which seems to work quite well for my usage. Your mileage may vary.

## Similar alternatives

A quick research led me to this alternative:

- https://www.rsyslog.com/doc/tutorials/database.html


It does not match exactly what I want for my specific setup. Basically, my syslog server is not really configurable because it's provided by my QNAP NAS, and I wanted to avoid any customization unsupported by QNAP. Also, I think logging to files is the way to go for best I/O efficiency, then bulk importing to SQL a syslog file after its rollover is the most efficient way to limit I/O and CPU consumption on my NAS. The downside is that copy from file to SQL is not done in real time, but I did not need that. It's also possible to run file rollover on a lower size threshold like 1 MB.

## Limitations

### Supports only PostgreSql

The tool supports only PostgreSql because that was my use case, and I use specific features of PostgreSql to optimize performances (bulk copy with binary `COPY` command, partitioned table by day...).

If you want to add an indirection level to support other database types, feel free to fork this project, and even make a *Pull Request*.

### Build depends on a missing Nuget package SyslogDecode

There is unfortunately no official Nuget package [SyslogDecode](https://github.com/microsoft/SyslogDecode). I built it and pushed it into a private Nuget feed so I could easily add a reference from this project. You can do the same if you need to build this project (build of [SyslogDecode](https://github.com/microsoft/SyslogDecode) already generates a Nuget package, you just have to store it yourself). 

You'll typically need to adapt file `src/nuget.config` because my private Nuget feed is not exposed on Internet. I left this file in this public repository because it simplifies building of my image for Docker hub (I use *Kaniko* project for that). Best would be to publish official package for [SyslogDecode](https://github.com/microsoft/SyslogDecode) but it seems this project has been inactive for a long time.

Also, I had to make a [fork of SyslogDecode](https://github.com/eric-b/SyslogDecode) to change the way it handles syslog timestamps. Original library heavily relies on `DateTime` and normalizes times to UTC. Because some syslog data may have inconsistencies on year, I needed to get the original timestamp to apply easy workarounds. So I changed *SyslogDecode* to use `DateTimeOffset` instead of `DateTime`, and to keep unchanged original offset read from syslog files.

Also, my quick dig into *SyslogDecode* made me to suspect some possible bugs on time parsing, depending on timezone written to syslog file. As far as I see, I'm not concerned, but it may be an issue for others.

## First run

### Create an empty database

You need to create an empty database, then adapt configuration settings (`appsettings.json`) with a valid connection string.

The program will automatically create database objects (tables, procedures...) at startup.

### Configuration

See `appsettings.json` file (in main project `SyslogFilesToSql`). I hope it's self explanatory.

Before enabling `CompressAfterImport`, if you want to, I suggest to give a first try. If all run well, you can enable it later. Program will automatically compress files already processed.

For the first run, you may need to set a high *Command Timeout* in the connection string if you have lots of files to import. Alternatively, you can move syslog files to import in a temporary directory if you have hundreds or thousands of them, and move files by batches of `x` at a time (just for the first run).

## Run in Kubernetes

You can use this image (linux/amd64): https://hub.docker.com/layers/eric1901/syslog-files-to-sql

Example of deployment manifest:

- This example relies on a secret named `syslog-files-to-sql-secret` containing your db password in key `db-password` which will be mounted as a file in the pod.
- You will also need to adapt:
  - `server` and `path` for NFS path containing syslog files.
  - Environment variable `Db__ConnectionString` with your connection string.

```
apiVersion: apps/v1
kind: Deployment
metadata:
  name: syslog-files-to-sql-deployment
  labels:
    app: syslog-files-to-sql
spec:
  replicas: 1
  selector:
    matchLabels:
      app: syslog-files-to-sql
  template:
    metadata:
      labels:
        app: syslog-files-to-sql
    spec:
      volumes:
      - name: secret-volume
        secret:
          secretName: syslog-files-to-sql-secret
      - name: syslog-volume
        nfs:
          server: syslog-server
          path: /syslog
      containers:
      - name: syslog-files-to-sql
        image: eric1901/syslog-files-to-sql:1.0.4
        volumeMounts:
        - name: secret-volume
          readOnly: true
          mountPath: "/var/secret/"
        - name: syslog-volume
          mountPath: "/var/syslog"
        env:
        - name: Db__ConnectionString
          value: "Host=pgsql-server;Port=5432;Database=syslog;Username=syslog_app;Maximum Pool Size=5;Command Timeout=1800;Include Error Detail=true"
        - name: Db__PasswordFile
          value: "/var/secret/db-password"
        - name: Db__MaxDaysToKeep
          value: "180"
        - name: Syslog__SyslogDirectory
          value: "/var/syslog"
        - name: Syslog__CompressAfterImport
          value: "false"
```

