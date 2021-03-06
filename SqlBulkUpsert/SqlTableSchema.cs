﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using SqlBulkUpsert.Properties;

namespace SqlBulkUpsert
{
	public class SqlTableSchema
	{
		private readonly List<Column> _columns = new List<Column>();
		private readonly List<Column> _primaryKeyColumns = new List<Column>();

		internal SqlTableSchema(string tableName)
		{
			if (tableName == null) throw new ArgumentNullException("tableName");
			TableName = tableName;
		}

		internal SqlTableSchema(string tableName, IEnumerable<Column> columns)
			: this(tableName)
		{
			if (columns == null) throw new ArgumentNullException("columns");
			_columns = columns.ToList();
		}

		internal SqlTableSchema(string tableName, IEnumerable<Column> columns, IEnumerable<string> primaryKeyColumns, string identityColumn)
			: this(tableName)
		{
			if (columns == null) throw new ArgumentNullException("columns");
			if (primaryKeyColumns == null) throw new ArgumentNullException("primaryKeyColumns");
			if (identityColumn == null) throw new ArgumentNullException("identityColumn");
			_columns = columns.ToList();
			_primaryKeyColumns = Check(primaryKeyColumns);
			IdentityColumn = _columns.First(c => c.Name == identityColumn);
			
			foreach (var primaryKeyColumn in _primaryKeyColumns)
			{
				primaryKeyColumn.CanBeUpdated = false;
			}

			IdentityColumn.CanBeInserted = false;
			IdentityColumn.CanBeUpdated = false;
		}

		private List<Column> Check(IEnumerable<string> primaryKeyColumns)
		{
			return primaryKeyColumns
				.Select(columnName => _columns.First(c => c.Name == columnName))
				.ToList();
		}

		public IEnumerable<Column> Columns
		{
			get { return _columns.AsEnumerable(); }
		}

		public IEnumerable<Column> PrimaryKeyColumns
		{
			get { return _primaryKeyColumns.AsEnumerable(); }
		}

		public Column IdentityColumn { get; private set; }

		public string TableName { get; private set; }
      
		public static SqlTableSchema LoadFromDatabase(SqlConnection connection, string tableName, string identityColumn)
		{
			if (connection == null) throw new ArgumentNullException("connection");
			if (tableName == null) throw new ArgumentNullException("tableName");
         
			using (var sqlCommand = connection.CreateCommand())
			{
				sqlCommand.CommandText = Resources.GetTableInfo;
				sqlCommand.Parameters.Add("@tableName", SqlDbType.VarChar).Value = tableName;
				using (var sqlDataReader = sqlCommand.ExecuteReader())
				{
					if (!sqlDataReader.Read())
					{
						throw new Exception("Table not found");
					}

					sqlDataReader.NextResult();

					return LoadFromReader(tableName, sqlDataReader, identityColumn);
				}
			}
		}

		internal static SqlTableSchema LoadFromReader(string tableName, IDataReader sqlDataReader, string identityColumn)
		{
			var columns = new List<Column>();
			var primaryKeyColumns = new List<string>();

			while (sqlDataReader.Read())
			{
				var column = Column.CreateFromReader(sqlDataReader);
				columns.Add(column);
			}

			sqlDataReader.NextResult();

			while (sqlDataReader.Read())
			{
				var columnName = (string)sqlDataReader["COLUMN_NAME"];
				primaryKeyColumns.Add(columnName);
			}

			return new SqlTableSchema(tableName, columns, primaryKeyColumns, identityColumn);
		}

		public string ToCreateTableCommandText()
		{
			return String.Format("CREATE TABLE {0} ({1})", TableName, Columns.ToColumnDefinitionListString());
		}

		public string ToDropTableCommandText()
		{
			return String.Format("DROP TABLE {0}", TableName);
		}
	}
}