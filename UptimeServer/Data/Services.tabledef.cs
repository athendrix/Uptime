using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using CSL.SQL;

namespace UptimeServer.Data
{
    public record Services(string Name, string Address, bool External, string? Backend, string? DisplayAddress, bool TrustCertificate, CheckType CheckType) : IDBSet
    {
        #region Static Functions
        public static Task<int> CreateDB(SQLDB sql) => sql.ExecuteNonQuery(
            "CREATE TABLE IF NOT EXISTS \"Services\" (" +
            "\"Name\" TEXT NOT NULL, " +
            "\"Address\" TEXT NOT NULL, " +
            "\"External\" BOOLEAN NOT NULL, " +
            "\"Backend\" TEXT, " +
            "\"DisplayAddress\" TEXT, " +
            "\"TrustCertificate\" BOOLEAN NOT NULL, " +
            "\"CheckType\" BIGINT NOT NULL, " +
            "PRIMARY KEY(\"Name\")" +
            ");");
        public static IEnumerable<Services> GetRecords(IDataReader dr)
        {
            while(dr.Read())
            {
                string Name =  (string)dr[0];
                string Address =  (string)dr[1];
                bool External =  (bool)dr[2];
                string? Backend = dr.IsDBNull(3) ? null : (string)dr[3];
                string? DisplayAddress = dr.IsDBNull(4) ? null : (string)dr[4];
                bool TrustCertificate =  (bool)dr[5];
                long _CheckType =  (long)dr[6];
                CheckType CheckType = (CheckType)_CheckType;
                yield return new Services(Name, Address, External, Backend, DisplayAddress, TrustCertificate, CheckType);
            }
            yield break;
        }
        #region Select
        public static async Task<AutoClosingEnumerable<Services>> Select(SQLDB sql)
        {
            AutoClosingDataReader dr = await sql.ExecuteReader("SELECT * FROM \"Services\";");
            return new AutoClosingEnumerable<Services>(GetRecords(dr),dr);
        }
        public static async Task<AutoClosingEnumerable<Services>> Select(SQLDB sql, string query, params object[] parameters)
        {
            AutoClosingDataReader dr = await sql.ExecuteReader("SELECT * FROM \"Services\" WHERE " + query + " ;", parameters);
            return new AutoClosingEnumerable<Services>(GetRecords(dr),dr);
        }
        public static async Task<Services?> SelectBy_Name(SQLDB sql, string Name)
        {
            using(AutoClosingDataReader dr = await sql.ExecuteReader("SELECT * FROM \"Services\" WHERE \"Name\" = @0;", Name))
            {
                return GetRecords(dr).FirstOrDefault();
            }
        }
        #endregion
        #region Delete
        public static Task<int> DeleteBy_Name(SQLDB sql, string Name) => sql.ExecuteNonQuery("DELETE FROM \"Services\" WHERE \"Name\" = @0;", Name);
        #endregion
        #region Table Management
        public static Task Truncate(SQLDB sql, bool cascade = false) => sql.ExecuteNonQuery($"TRUNCATE \"Services\"{(cascade?" CASCADE":"")};");
        public static Task Drop(SQLDB sql, bool cascade = false) => sql.ExecuteNonQuery($"DROP TABLE IF EXISTS \"Services\"{(cascade?" CASCADE":"")};");
        #endregion
        #endregion
        #region Instance Functions
        public Task<int> Insert(SQLDB sql) =>
            sql.ExecuteNonQuery("INSERT INTO \"Services\" (\"Name\", \"Address\", \"External\", \"Backend\", \"DisplayAddress\", \"TrustCertificate\", \"CheckType\") " +
            "VALUES(@0, @1, @2, @3, @4, @5, @6);", ToArray());
        public Task<int> Update(SQLDB sql) =>
            sql.ExecuteNonQuery("UPDATE \"Services\" " +
            "SET \"Address\" = @1, \"External\" = @2, \"Backend\" = @3, \"DisplayAddress\" = @4, \"TrustCertificate\" = @5, \"CheckType\" = @6 " +
            "WHERE \"Name\" = @0;", ToArray());
        public Task<int> Upsert(SQLDB sql) =>
            sql.ExecuteNonQuery("INSERT INTO \"Services\" (\"Name\", \"Address\", \"External\", \"Backend\", \"DisplayAddress\", \"TrustCertificate\", \"CheckType\") " +
            "VALUES(@0, @1, @2, @3, @4, @5, @6) " +
            "ON CONFLICT (\"Name\") DO UPDATE " +
            "SET \"Address\" = @1, \"External\" = @2, \"Backend\" = @3, \"DisplayAddress\" = @4, \"TrustCertificate\" = @5, \"CheckType\" = @6;", ToArray());
        public object?[] ToArray()
        {
            string _Name = Name;
            string _Address = Address;
            bool _External = External;
            string? _Backend = Backend == null ? default : Backend;
            string? _DisplayAddress = DisplayAddress == null ? default : DisplayAddress;
            bool _TrustCertificate = TrustCertificate;
            long _CheckType = (long)CheckType;
            return new object?[] { _Name, _Address, _External, _Backend, _DisplayAddress, _TrustCertificate, _CheckType };
        }
        #endregion
    }
    #region Example Enums
    
    ////Example Enum
    //[Flags]
    ////Specifying ulong allows data to be auto converted for your convenience into the database.
    //public enum CheckType : ulong
    //{
        //NoFlags = 0,
        //Flag1   = 1UL << 0,
        //Flag2   = 1UL << 1,
        //Flag3   = 1UL << 2,
        //Flag4   = 1UL << 3,
        //Flag5   = 1UL << 4,
        //Flag6   = 1UL << 5,
        //Flag7   = 1UL << 6,
        //Flag8   = 1UL << 7,
        //Flag9   = 1UL << 8,
        //Flag10  = 1UL << 9,
        //Flag11  = 1UL << 10,
        //Flag12  = 1UL << 11,
        //Flag13  = 1UL << 12,
        //Flag14  = 1UL << 13,
        //Flag15  = 1UL << 14,
        //Flag16  = 1UL << 15,
        //Flag17  = 1UL << 16,
        //Flag18  = 1UL << 17,
        //Flag19  = 1UL << 18,
        //Flag20  = 1UL << 19,
        //Flag21  = 1UL << 20,
        //Flag22  = 1UL << 21,
        //Flag23  = 1UL << 22,
        //Flag24  = 1UL << 23,
        //Flag25  = 1UL << 24,
        //Flag26  = 1UL << 25,
        //Flag27  = 1UL << 26,
        //Flag28  = 1UL << 27,
        //Flag29  = 1UL << 28,
        //Flag30  = 1UL << 29,
        //Flag31  = 1UL << 30,
        //Flag32  = 1UL << 31,
        //Flag33  = 1UL << 32,
        //Flag34  = 1UL << 33,
        //Flag35  = 1UL << 34,
        //Flag36  = 1UL << 35,
        //Flag37  = 1UL << 36,
        //Flag38  = 1UL << 37,
        //Flag39  = 1UL << 38,
        //Flag40  = 1UL << 39,
        //Flag41  = 1UL << 40,
        //Flag42  = 1UL << 41,
        //Flag43  = 1UL << 42,
        //Flag44  = 1UL << 43,
        //Flag45  = 1UL << 44,
        //Flag46  = 1UL << 45,
        //Flag47  = 1UL << 46,
        //Flag48  = 1UL << 47,
        //Flag49  = 1UL << 48,
        //Flag50  = 1UL << 49,
        //Flag51  = 1UL << 50,
        //Flag52  = 1UL << 51,
        //Flag53  = 1UL << 52,
        //Flag54  = 1UL << 53,
        //Flag55  = 1UL << 54,
        //Flag56  = 1UL << 55,
        //Flag57  = 1UL << 56,
        //Flag58  = 1UL << 57,
        //Flag59  = 1UL << 58,
        //Flag60  = 1UL << 59,
        //Flag61  = 1UL << 60,
        //Flag62  = 1UL << 61,
        //Flag63  = 1UL << 62,
        //Flag64  = 1UL << 63,
    //}
    #endregion
}