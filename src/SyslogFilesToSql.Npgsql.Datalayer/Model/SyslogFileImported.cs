namespace SyslogFilesToSql.Npgsql.Datalayer.Model
{
    public sealed class SyslogFileImported
    {
        public int Id { get; set; }

        public byte[] FileHash { get; set; } = default!;

        public bool IsComplete { get; set; }

        public string FilePath { get; set; } = default!;
    }
}
