// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using SQLitePCL;

namespace Microsoft.Data.Sqlite
{
    internal class SqliteDataRecord : SqliteValueReader
    {
        private readonly sqlite3_stmt _stmt;
        private readonly SqliteConnection _connection;
        private readonly byte[][] _blobCache;

        public SqliteDataRecord(sqlite3_stmt stmt, SqliteConnection connection)
        {
            _stmt = stmt;
            _connection = connection;
            _blobCache = new byte[FieldCount][];
        }

        public virtual object this[string name]
            => GetValue(GetOrdinal(name));

        public virtual object this[int ordinal]
            => GetValue(ordinal);

        public override int FieldCount
            => raw.sqlite3_column_count(_stmt);

        protected override double GetDoubleCore(int ordinal)
            => raw.sqlite3_column_double(_stmt, ordinal);

        protected override long GetInt64Core(int ordinal)
            => raw.sqlite3_column_int64(_stmt, ordinal);

        protected override string GetStringCore(int ordinal)
            => raw.sqlite3_column_text(_stmt, ordinal);

        protected override byte[] GetBlobCore(int ordinal)
            => raw.sqlite3_column_blob(_stmt, ordinal);

        protected override int GetSqliteType(int ordinal)
        {
            var type = raw.sqlite3_column_type(_stmt, ordinal);
            if (type == raw.SQLITE_NULL
                && (ordinal < 0 || ordinal >= FieldCount))
            {
                // NB: Message is provided by the framework
                throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, message: null);
            }

            return type;
        }

        protected override T GetNull<T>(int ordinal)
            => typeof(T) == typeof(DBNull) || typeof(T) == typeof(object)
                ? (T)(object)DBNull.Value
                : throw new InvalidOperationException(GetOnNullErrorMsg(ordinal));

        public virtual string GetName(int ordinal)
        {
            var name = raw.sqlite3_column_name(_stmt, ordinal);
            if (name == null
                && (ordinal < 0 || ordinal >= FieldCount))
            {
                // NB: Message is provided by the framework
                throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, message: null);
            }

            return name;
        }

        public virtual int GetOrdinal(string name)
        {
            for (var i = 0; i < FieldCount; i++)
            {
                if (GetName(i) == name)
                {
                    return i;
                }
            }

            // NB: Message is provided by framework
            throw new ArgumentOutOfRangeException(nameof(name), name, message: null);
        }

        public virtual string GetDataTypeName(int ordinal)
        {
            var typeName = raw.sqlite3_column_decltype(_stmt, ordinal);
            if (typeName != null)
            {
                var i = typeName.IndexOf('(');

                return i == -1
                    ? typeName
                    : typeName.Substring(0, i);
            }

            var sqliteType = GetSqliteType(ordinal);
            switch (sqliteType)
            {
                case raw.SQLITE_INTEGER:
                    return "INTEGER";

                case raw.SQLITE_FLOAT:
                    return "REAL";

                case raw.SQLITE_TEXT:
                    return "TEXT";

                case raw.SQLITE_BLOB:
                    return "BLOB";

                case raw.SQLITE_NULL:
                    return "BLOB"; // since no type is specified the column has affinity BLOB

                default:
                    Debug.Assert(false, "Unexpected column type: " + sqliteType);
                    return "INTEGER";
            }
        }

        public virtual Type GetFieldType(int ordinal)
        {
            var sqliteType = GetSqliteType(ordinal);
            if (sqliteType == raw.SQLITE_NULL)
            {
                sqliteType = GetColumnAffinity(ordinal);
            }

            return GetFieldTypeFromSqliteType(sqliteType);
        }

        internal static Type GetFieldTypeFromSqliteType(int sqliteType)
        {
            switch (sqliteType)
            {
                case raw.SQLITE_INTEGER:
                    return typeof(long);

                case raw.SQLITE_FLOAT:
                    return typeof(double);

                case raw.SQLITE_TEXT:
                    return typeof(string);

                case raw.SQLITE_BLOB:
                    return typeof(byte[]);

                case raw.SQLITE_NULL:
                    return typeof(int);

                default:
                    Debug.Assert(false, "Unexpected column type: " + sqliteType);
                    return typeof(int);
            }
        }

        public static Type GetFieldType(string type)
        {
            switch (type)
            {
                case "integer":
                    return typeof(long);

                case "real":
                    return typeof(double);

                case "text":
                    return typeof(string);

                case "blob":
                    return typeof(byte[]);

                case null:
                    return typeof(int);

                default:
                    Debug.Assert(false, "Unexpected column type: " + type);
                    return typeof(int);
            }
        }

        public virtual long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            var blob = GetCachedBlob(ordinal);

            long bytesToRead = (long)blob.Length - dataOffset;
            if (buffer != null)
            {
                bytesToRead = System.Math.Min(bytesToRead, length);
                Array.Copy(blob, dataOffset, buffer, bufferOffset, bytesToRead);
            }
            return bytesToRead;
        }

        public virtual long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            var text = GetString(ordinal);

            int charsToRead = text.Length - (int)dataOffset;
            charsToRead = System.Math.Min(charsToRead, length);
            text.CopyTo((int)dataOffset, buffer, bufferOffset, charsToRead);
            return charsToRead;
        }

        public virtual Stream GetStream(int ordinal)
        {
            if (ordinal < 0 || ordinal >= FieldCount)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, message: null);
            }

            var blobDatabaseName = raw.sqlite3_column_database_name(_stmt, ordinal);
            var blobTableName = raw.sqlite3_column_table_name(_stmt, ordinal);

            var rowidOrdinal = -1;
            for (var i = 0; i < FieldCount; i++)
            {
                if (i == ordinal)
                {
                    continue;
                }

                var databaseName = raw.sqlite3_column_database_name(_stmt, i);
                if (databaseName != blobDatabaseName)
                {
                    continue;
                }

                var tableName = raw.sqlite3_column_table_name(_stmt, i);
                if (tableName != blobTableName)
                {
                    continue;
                }

                var columnName = raw.sqlite3_column_origin_name(_stmt, i);
                if ((columnName == "rowid") || (columnName == "_rowid_") || (columnName == "oid"))
                {
                    rowidOrdinal = i;
                    break;
                }

                var rc = raw.sqlite3_table_column_metadata(
                    _connection.Handle,
                    databaseName,
                    tableName,
                    columnName,
                    out var dataType,
                    out var collSeq,
                    out var notNull,
                    out var primaryKey,
                    out var autoInc);
                SqliteException.ThrowExceptionForRC(rc, _connection.Handle);
                if ((dataType == "INTEGER") && (primaryKey != 0))
                {
                    rowidOrdinal = i;
                    break;
                }
            }

            if (rowidOrdinal < 0)
            {
                return new MemoryStream(GetCachedBlob(ordinal), false);
            }

            var blobColumnName = raw.sqlite3_column_origin_name(_stmt, ordinal);
            var rowid = GetInt32(rowidOrdinal);

            return new SqliteBlob(_connection, blobTableName, blobColumnName, rowid, readOnly: true);
        }

        internal void Clear()
        {
            for (var i = 0; i < _blobCache.Length; i++)
            {
                _blobCache[i] = null;
            }
        }

        private byte[] GetCachedBlob(int ordinal)
        {
            if (ordinal < 0 || ordinal >= FieldCount)
            {
                // NB: Message is provided by the framework
                throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, message: null);
            }

            var blob = _blobCache[ordinal];
            if (blob == null)
            {
                blob = GetBlob(ordinal);
                _blobCache[ordinal] = blob;
            }

            return blob;
        }

        private int GetColumnAffinity(int ordinal)
        {
            var columnType = raw.sqlite3_column_decltype(_stmt, ordinal);
            return Sqlite3AffinityType(columnType);
        }

        internal static int Sqlite3AffinityType(string dataTypeName)
        {
            if (dataTypeName == null)
            {
                // if no type is specified then the column has affinity BLOB
                return raw.SQLITE_BLOB;
            }

            uint h = 0;
            var aff = -1; // -1 for NUMERICAL affinity

            var idx = 0;
            while (idx < dataTypeName.Length)
            {
                h = (h << 8) + char.ToLower(dataTypeName[idx], CultureInfo.InvariantCulture);
                idx++;
                if (h == (('c' << 24) + ('h' << 16) + ('a' << 8) + 'r')) // CHAR
                {
                    aff = raw.SQLITE_TEXT;
                }
                else if (h == (('c' << 24) + ('l' << 16) + ('o' << 8) + 'b')) // CLOB
                {
                    aff = raw.SQLITE_TEXT;
                }
                else if (h == (('t' << 24) + ('e' << 16) + ('x' << 8) + 't')) // TEXT
                {
                    aff = raw.SQLITE_TEXT;
                }
                else if (h == (('b' << 24) + ('l' << 16) + ('o' << 8) + 'b') // BLOB
                   && (aff == -1 || aff == raw.SQLITE_FLOAT))
                {
                    aff = raw.SQLITE_BLOB;
                }
                else if (h == (('r' << 24) + ('e' << 16) + ('a' << 8) + 'l') // REAL
                   && aff == -1)
                {
                    aff = raw.SQLITE_FLOAT;
                }
                else if (h == (('f' << 24) + ('l' << 16) + ('o' << 8) + 'a') // FLOA
                   && aff == -1)
                {
                    aff = raw.SQLITE_FLOAT;
                }
                else if (h == (('d' << 24) + ('o' << 16) + ('u' << 8) + 'b') // DOUB
                   && aff == -1)
                {
                    aff = raw.SQLITE_FLOAT;
                }
                else if ((h & 0x00FFFFFF) == (('i' << 16) + ('n' << 8) + 't')) // INT
                {
                    aff = raw.SQLITE_INTEGER;
                    break;
                }
            }

            return aff != -1 ? aff : raw.SQLITE_TEXT; // code NUMERICAL affinity as TEXT
        }
    }
}
