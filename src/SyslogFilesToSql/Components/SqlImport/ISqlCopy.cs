using System;

namespace SyslogFilesToSql.Components.SqlImport
{ 
    internal interface ISqlCopy
    {
        void Write(SqlCopyRow row);
    }

    internal record class SqlCopyRow(byte[] fileHash,
                                   string facility,
                                   string severity,
                                   DateTime createdOn,
                                   string host,
                                   string? app,
                                   int? pid,
                                   int? msgId,
                                   string msg,
                                   string payloadType);
}
