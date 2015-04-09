using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using TShockAPI;
using TShockAPI.DB;

namespace InfiniteInventories
{
	public static class SqlUtils
	{
		public static int InsertOrIgnore(this IDbConnection db, String tableName, String[] fieldNames, object[] args)
		{
			String queryString = "";
			var fields = String.Join(", ", fieldNames);
			var values = String.Join(", ", args.Select((a, i) => "@" + i));
			if (db.GetSqlType() == SqlType.Mysql)
			{
				queryString = "INSERT IGNORE INTO {2} ({0}) VALUES ({1})".SFormat(fields, values, tableName);
			}
			else if (db.GetSqlType() == SqlType.Sqlite)
			{
				queryString = "INSERT OR IGNORE INTO {2} ({0}) VALUES ({1})".SFormat(fields, values, tableName);
			}
			else
			{
				throw new NotSupportedException("The SQL format {0} is not currently supported.".SFormat(db.GetSqlType().ToString()));
			}
			return db.Query(queryString, args);
		}
	}
}
