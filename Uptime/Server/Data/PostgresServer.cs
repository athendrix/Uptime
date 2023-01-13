using CSL.SQL;

namespace Uptime.Server.Data
{
    public static class PostgresServer
    {
        static PostgresServer()
        {
            CSL.DependencyInjection.NpgsqlConnectionConstructor = (x) => new Npgsql.NpgsqlConnection(x);
            CSL.DependencyInjection.NpgsqlConnectionStringConstructor = () => new Npgsql.NpgsqlConnectionStringBuilder();
            CSL.DependencyInjection.SslModeConverter = (x) => Enum.Parse(typeof(Npgsql.SslMode), x.ToString());
            PostgreSQL.TrustAllServerCertificates = true;
        }
        public static async Task<SQLDB> GetSQL() => await PostgreSQL.Connect
        (
            Environment.GetEnvironmentVariable("SERVER") ?? "localhost",
            Environment.GetEnvironmentVariable("DATABASE") ?? Environment.GetEnvironmentVariable("USERNAME") ?? "postgres",
            Environment.GetEnvironmentVariable("USERNAME") ?? "postgres",
            Environment.GetEnvironmentVariable("PASSWORD") ?? throw new ArgumentException("No database PASSWORD specified in the environment!", "PASSWORD"),
            Environment.GetEnvironmentVariable("SCHEMA")
        );
        //public static ServicesFactory Services = new ServicesFactory();
    }
}