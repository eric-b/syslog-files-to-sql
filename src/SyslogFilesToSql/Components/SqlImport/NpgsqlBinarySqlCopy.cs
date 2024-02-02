using System;

namespace SyslogFilesToSql.Components.SqlImport
{
    sealed class NpgsqlBinarySqlCopy(global::Npgsql.NpgsqlBinaryImporter importer) : ISqlCopy
    {
        public void Write(SqlCopyRow row)
        {
            if (row is null)
            {
                throw new ArgumentNullException(nameof(row));
            }
            importer.StartRow();
            importer.Write(row.fileHash, NpgsqlTypes.NpgsqlDbType.Bytea);
            importer.Write(row.facility);
            importer.Write(row.severity);
            importer.Write(row.createdOn, NpgsqlTypes.NpgsqlDbType.Timestamp);
            importer.Write(row.host);
            importer.Write(row.payloadType);
            importer.Write(row.app);
            importer.Write(row.pid);
            importer.Write(row.msgId);
            importer.Write(row.msg);
        }
    }
}
