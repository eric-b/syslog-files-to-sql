using SyslogFilesToSql.Components.SqlImport;
using System;
using System.Collections.Generic;

namespace SyslogFilesToSql.Tests
{
    sealed class SqlCopy : ISqlCopy
    {
        private readonly List<SqlCopyRow> _rows = new List<SqlCopyRow>();

        public IReadOnlyList<SqlCopyRow> Rows => _rows;

        public void Write(SqlCopyRow row)
        {
            _rows.Add(row);
        }
    }
}
