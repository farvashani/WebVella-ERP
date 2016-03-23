﻿using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using WebVella.ERP.Api.Models;
using WebVella.ERP.Database.Models;

namespace WebVella.ERP.Database
{
	public static class DbRepository
	{
		public static void CreateTable(string name)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"CREATE TABLE {name} ();";

				NpgsqlCommand command = connection.CreateCommand(sql);

				command.ExecuteNonQuery();
			}
		}

		public static void RenameTable(string name, string newName)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"ALTER TABLE {name} RENAME TO {newName};";

				NpgsqlCommand command = connection.CreateCommand(sql);

				command.ExecuteNonQuery();
			}
		}

		public static void DeleteTable(string name, bool cascade = false)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				string cascadeCommand = cascade ? " CASCADE" : "";
				string sql = $"DROP TABLE IF EXISTS {name}{cascadeCommand};";

				NpgsqlCommand command = connection.CreateCommand(sql);

				command.ExecuteNonQuery();
			}
		}

		public static void CreateColumn(string tableName, string name, FieldType type, bool isPrimaryKey, object defaultValue, bool isNullable = false)
		{
			string pgType = DbTypeConverter.ConvertToDatabaseSqlType(type);

			using (var connection = DbContext.Current.CreateConnection())
			{
				NpgsqlCommand command = connection.CreateCommand("");

				string canBeNull = isNullable && !isPrimaryKey ? "NULL" : "NOT NULL";
				string sql = $"ALTER TABLE {tableName} ADD COLUMN {name} {pgType} {canBeNull}";

				if (defaultValue != null && !(defaultValue is Guid && (Guid)defaultValue == Guid.Empty))
				{
					//var parameter = command.CreateParameter() as NpgsqlParameter;
					//parameter.ParameterName = "@default_value";
					//parameter.Value = defaultValue;
					//parameter.NpgsqlDbType = DbTypeConverter.ConvertToDatabaseType(type);
					//command.Parameters.Add(parameter);
					var defVal = ConvertDefaultValue(type, defaultValue);
					sql += $" DEFAULT {defVal}";
				}

				if (isPrimaryKey)
					sql += $" PRIMARY KEY";

				sql += ";";

				command.CommandText = sql;

				command.ExecuteNonQuery();
			}
		}

		public static void RenameColumn(string tableName, string name, string newName)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"ALTER TABLE {tableName} RENAME COLUMN {name} TO {newName};";

				NpgsqlCommand command = connection.CreateCommand(sql);

				command.ExecuteNonQuery();
			}
		}

		public static void DeleteColumn(string tableName, string name)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"ALTER TABLE {tableName} DROP COLUMN IF EXISTS {name};";

				NpgsqlCommand command = connection.CreateCommand(sql);

				command.ExecuteNonQuery();
			}
		}

		public static void SetPrimaryKey(string tableName, List<string> columns)
		{
			if (columns.Count == 0)
				return;

			string keyNames = "";
			foreach (var col in columns)
			{
				keyNames += col + ", ";
			}
			keyNames = keyNames.Remove(keyNames.Length - 2, 2);

			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"ALTER TABLE {tableName} ADD PRIMARY KEY ({keyNames});";

				NpgsqlCommand command = connection.CreateCommand(sql);

				command.ExecuteNonQuery();
			}
		}

		public static void SetUniqueConstraint(string constraintName, string tableName, List<string> columns)
		{
			if (columns.Count == 0)
				return;

			string colNames = "";
			foreach (var col in columns)
			{
				colNames += col + ", ";
			}
			colNames = colNames.Remove(colNames.Length - 2, 2);

			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"ALTER TABLE {tableName} ADD CONSTRAINT {constraintName} UNIQUE ({colNames});";

				NpgsqlCommand command = connection.CreateCommand(sql);

				command.ExecuteNonQuery();
			}
		}

		public static void CreateRelation(string relName, string originTableName, string originFieldName, string targetTableName, string targetFieldName)
		{
			if (!TableExists(originTableName))
				return;

			if (!TableExists(targetTableName))
				return;

			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"ALTER TABLE {targetTableName} ADD CONSTRAINT {relName} FOREIGN KEY ({targetFieldName}) REFERENCES {originTableName} ({originFieldName});";
				NpgsqlCommand command = connection.CreateCommand(sql);
				command.ExecuteNonQuery();
			}

			//if fields are not id field create index , for id we alread have by default
			if( originFieldName != "id" )
				CreateIndex($"idx_{relName}_{originFieldName}", originTableName, originFieldName);
			if (targetFieldName != "id")
				CreateIndex($"idx_{relName}_{originFieldName}", targetTableName, targetFieldName);

		}

		public static void CreateNtoNRelation(string relName, string originTableName, string originFieldName, string targetTableName, string targetFieldName)
		{
			string relTableName = $"rel_{relName}";
			CreateTable(relTableName);
			CreateColumn(relTableName, "origin_id", FieldType.GuidField, false, null, false);
			CreateColumn(relTableName, "target_id", FieldType.GuidField, false, null, false);
			SetPrimaryKey(relTableName, new List<string> { "origin_id", "target_id" });
			CreateRelation($"{relName}_origin", originTableName, originFieldName, relTableName, "origin_id");
			CreateRelation($"{relName}_target", targetTableName, targetFieldName, relTableName, "target_id");
		}

		// not used
		//public static void RenameRelation(string tableName, string name, string newName)
		//{
		//	if (!TableExists(tableName))
		//		return;

		//	using (var connection = DbContext.Current.CreateConnection())
		//	{
		//		string sql = $"ALTER TABLE {tableName} RENAME CONSTRAINT {name} TO {newName};";
		//		NpgsqlCommand command = connection.CreateCommand(sql);
		//		command.ExecuteNonQuery();
		//	}
		//}

		public static void DeleteRelation(string relName, string tableName)
		{
			if (!TableExists(tableName))
				return;

			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"ALTER TABLE {tableName} DROP CONSTRAINT IF EXISTS {relName};";
				NpgsqlCommand command = connection.CreateCommand(sql);
				command.ExecuteNonQuery();
			}

			DropIndex(relName);
		}

		public static void DeleteNtoNRelation(string relName, string originTableName, string targetTableName)
		{
			string relTableName = $"rel_{relName}";

			DeleteRelation($"{relName}_origin", originTableName);
			DeleteRelation($"{relName}_target", targetTableName);
			DeleteTable(relTableName, false);
		}

		public static void CreateIndex( string indexName, string tableName, string columnName, bool unique = false, bool ascending = true )
		{
			if (!TableExists(tableName))
				return;

			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"CREATE INDEX {indexName} ON {tableName} ({columnName}) ";
				if ( unique )
					sql = $"CREATE UNIQUE INDEX {indexName} ON {tableName} ({columnName}) ";

				if( !ascending )
					sql = sql + " DESC";

				NpgsqlCommand command = connection.CreateCommand(sql);
				command.ExecuteNonQuery();
			}
		}

		public static void DropIndex(string indexName )
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				string sql = $"DROP INDEX IF EXISTS {indexName}";
				NpgsqlCommand command = connection.CreateCommand(sql);
				command.ExecuteNonQuery();
			}
		}

		public static bool InsertRecord(string tableName, List<DbParameter> parameters)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				NpgsqlCommand command = connection.CreateCommand("");

				string columns = "";
				string values = "";
				foreach (var param in parameters)
				{

					var parameter = command.CreateParameter() as NpgsqlParameter;
					parameter.ParameterName = param.Name;
					parameter.Value = param.Value ?? DBNull.Value;
					parameter.NpgsqlDbType = param.Type;
					command.Parameters.Add(parameter);

					columns += $"{param.Name}, ";
					values += $"@{param.Name}, ";
				}

				columns = columns.Remove(columns.Length - 2, 2);
				values = values.Remove(values.Length - 2, 2);

				command.CommandText = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";

				return command.ExecuteNonQuery() > 0;
			}
		}

		public static bool UpdateRecord(string tableName, List<DbParameter> parameters)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				NpgsqlCommand command = connection.CreateCommand("");

				string values = "";
				foreach (var param in parameters)
				{

					var parameter = command.CreateParameter() as NpgsqlParameter;
					parameter.ParameterName = param.Name;
					parameter.Value = param.Value;
					parameter.NpgsqlDbType = param.Type;
					command.Parameters.Add(parameter);

					values += $"{param.Name}=@{param.Name}, ";
				}

				values = values.Remove(values.Length - 2, 2);

				command.CommandText = $"UPDATE {tableName} SET {values} WHERE id=@id";

				return command.ExecuteNonQuery() > 0;
			}
		}

		public static bool DeleteRecord(string tableName, Guid id)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				NpgsqlCommand command = connection.CreateCommand("");

				var parameter = command.CreateParameter() as NpgsqlParameter;
				parameter.ParameterName = "id";
				parameter.Value = id;
				parameter.NpgsqlDbType = NpgsqlDbType.Uuid;
				command.Parameters.Add(parameter);

				command.CommandText = $"DELETE FROM {tableName} WHERE id=@id";

				return command.ExecuteNonQuery() > 0;
			}
		}

		public static bool TableExists(string tableName)
		{
			using (var connection = DbContext.Current.CreateConnection())
			{
				bool tableExists = false;
				var command = connection.CreateCommand($"SELECT EXISTS (  SELECT 1 FROM   information_schema.tables  WHERE  table_schema = 'public' AND table_name = '{tableName}' ) ");
				using (var reader = command.ExecuteReader())
				{
					reader.Read();
					tableExists = reader.GetBoolean(0);
					reader.Close();
				}
				return tableExists;
			}
		}

		public static string ConvertDefaultValue(FieldType type, object value)
		{
			if (value == null)
				return null;

			switch (type)
			{
				case FieldType.DateField:
					return "'" + ((DateTime)value).ToString("yyyy-MM-dd") + "'";
				case FieldType.DateTimeField:
					return "'" + ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss") + "'";
				case FieldType.EmailField:
				case FieldType.FileField:
				case FieldType.HtmlField:
				case FieldType.ImageField:
				case FieldType.MultiLineTextField:
				case FieldType.PhoneField:
				case FieldType.SelectField:
				case FieldType.TextField:
				case FieldType.UrlField:
					return "'" + value + "'";
				case FieldType.GuidField:
					return "'" + ((Guid)value).ToString() + "'";
				case FieldType.MultiSelectField:
					{
						string outValue = "";
						outValue += "'{";
						List<string> defaultValues = (List<string>)value;
						if (defaultValues.Count > 0)
						{
							foreach (var val in defaultValues)
							{
								outValue += $"\"{val}\",";
							}
							outValue = outValue.Remove(outValue.Length - 1, 1);
						}
						outValue += "}'";
						return outValue;
					}
				default:
					return value.ToString();
			}
		}

	}
}
