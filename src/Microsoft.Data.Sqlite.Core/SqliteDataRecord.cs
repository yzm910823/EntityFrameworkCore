// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite.Properties;
using SQLitePCL;

using static SQLitePCL.raw;

namespace Microsoft.Data.Sqlite
{
    internal class SqliteDataRecord : SqliteValueReader, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly byte[][] _blobCache;
        private bool _stepped;

        public SqliteDataRecord(sqlite3_stmt stmt, bool hasRows, SqliteConnection connection)
        {
            Handle = stmt;
            HasRows = hasRows;
            _connection = connection;
            _blobCache = new byte[FieldCount][];
        }

        public virtual object this[string name]
            => GetValue(GetOrdinal(name));

        public virtual object this[int ordinal]
            => GetValue(ordinal);

        public override int FieldCount
            => sqlite3_column_count(Handle);

        public sqlite3_stmt Handle { get; }

        public bool HasRows { get; }

        public override bool IsDBNull(int ordinal)
            => !_stepped || sqlite3_data_count(Handle) == 0
                ? throw new InvalidOperationException(Resources.NoData)
                : base.IsDBNull(ordinal);

        public override object GetValue(int ordinal)
            => !_stepped || sqlite3_data_count(Handle) == 0
                ? throw new InvalidOperationException(Resources.NoData)
                : base.GetValue(ordinal);

        protected override double GetDoubleCore(int ordinal)
            => sqlite3_column_double(Handle, ordinal);

        protected override long GetInt64Core(int ordinal)
            => sqlite3_column_int64(Handle, ordinal);

        protected override string GetStringCore(int ordinal)
            => sqlite3_column_text(Handle, ordinal);

        protected override byte[] GetBlobCore(int ordinal)
            => sqlite3_column_blob(Handle, ordinal);

        protected override int GetSqliteType(int ordinal)
        {
            var type = sqlite3_column_type(Handle, ordinal);
            if (type == SQLITE_NULL
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
            var name = sqlite3_column_name(Handle, ordinal);
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
            var typeName = sqlite3_column_decltype(Handle, ordinal);
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
                case SQLITE_INTEGER:
                    return "INTEGER";

                case SQLITE_FLOAT:
                    return "REAL";

                case SQLITE_TEXT:
                    return "TEXT";

                case SQLITE_BLOB:
                    return "BLOB";

                case SQLITE_NULL:
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
                case SQLITE_INTEGER:
                    return typeof(long);

                case SQLITE_FLOAT:
                    return typeof(double);

                case SQLITE_TEXT:
                    return typeof(string);

                case SQLITE_BLOB:
                    return typeof(byte[]);

                case SQLITE_NULL:
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

            long bytesToRead = blob.Length - dataOffset;
            if (buffer != null)
            {
                bytesToRead = Math.Min(bytesToRead, length);
                Array.Copy(blob, dataOffset, buffer, bufferOffset, bytesToRead);
            }

            return bytesToRead;
        }

        public virtual long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            var text = GetString(ordinal);

            int charsToRead = text.Length - (int)dataOffset;
            charsToRead = Math.Min(charsToRead, length);
            text.CopyTo((int)dataOffset, buffer, bufferOffset, charsToRead);
            return charsToRead;
        }

        public virtual Stream GetStream(int ordinal)
        {
            if (ordinal < 0
                || ordinal >= FieldCount)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal), ordinal, message: null);
            }

            var blobDatabaseName = sqlite3_column_database_name(Handle, ordinal);
            var blobTableName = sqlite3_column_table_name(Handle, ordinal);

            var rowidOrdinal = -1;
            for (var i = 0; i < FieldCount; i++)
            {
                if (i == ordinal)
                {
                    continue;
                }

                var databaseName = sqlite3_column_database_name(Handle, i);
                if (databaseName != blobDatabaseName)
                {
                    continue;
                }

                var tableName = sqlite3_column_table_name(Handle, i);
                if (tableName != blobTableName)
                {
                    continue;
                }

                var columnName = sqlite3_column_origin_name(Handle, i);
                if (columnName == "rowid")
                {
                    rowidOrdinal = i;
                    break;
                }

                var rc = sqlite3_table_column_metadata(
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
                if (string.Equals(dataType, "INTEGER", StringComparison.OrdinalIgnoreCase)
                    && primaryKey != 0)
                {
                    rowidOrdinal = i;
                    break;
                }
            }

            if (rowidOrdinal < 0)
            {
                return new MemoryStream(GetCachedBlob(ordinal), false);
            }

            var blobColumnName = sqlite3_column_origin_name(Handle, ordinal);
            var rowid = GetInt32(rowidOrdinal);

            return new SqliteBlob(_connection, blobTableName, blobColumnName, rowid, readOnly: true);
        }

        public bool Read()
        {
            if (!_stepped)
            {
                _stepped = true;

                return HasRows;
            }

            var rc = sqlite3_step(Handle);
            SqliteException.ThrowExceptionForRC(rc, _connection.Handle);

            Array.Clear(_blobCache, 0, _blobCache.Length);

            return rc != SQLITE_DONE;
        }

        public void Dispose()
            => sqlite3_reset(Handle);

        private byte[] GetCachedBlob(int ordinal)
        {
            if (ordinal < 0
                || ordinal >= FieldCount)
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
