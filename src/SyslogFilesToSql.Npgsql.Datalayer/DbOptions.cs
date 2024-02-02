using System.Text;
using System;
using System.IO;

namespace SyslogFilesToSql.Npgsql.Datalayer
{
    public sealed class DbOptions
    {
        public string? ConnectionString { get; set; }

        public string? Password { get; set; }

        public string? PasswordFile { get; set; }

        private void Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new ArgumentException($"{nameof(ConnectionString)} must be set.");
            }

            if (string.IsNullOrEmpty(Password) && string.IsNullOrEmpty(PasswordFile))
            {
                throw new ArgumentException($"{nameof(Password)} or {nameof(PasswordFile)} must be set.");
            }
        }

        public string GetConnectionStringWithPassword()
        {
            Validate();
            var builder = new global::Npgsql.NpgsqlConnectionStringBuilder(ConnectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                throw new ArgumentException($"Password to access database must not be set in {nameof(ConnectionString)}. Instead, use {nameof(Password)} or {nameof(PasswordFile)}.");
            }
            builder.ApplicationName = "SyslogFilesToSql";
            if (!string.IsNullOrEmpty(PasswordFile))
            {
                builder.Password = File.ReadAllText(PasswordFile, Encoding.UTF8);
            }
            else
            {
                builder.Password = Password;
            }

            return builder.ConnectionString;
        }

    }
}
