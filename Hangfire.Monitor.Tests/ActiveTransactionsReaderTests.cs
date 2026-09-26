using System.Data;
using System.Data.Common;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Tests;

public class ActiveTransactionsReaderTests
{
    private readonly ActiveTransactionsReader _reader = new();

    [Fact]
    public void Build_SelectsActiveTransactions_ForCurrentDatabase()
    {
        var sql = ActiveTransactionsQuery.Build();

        Assert.Contains("sys.dm_tran_active_transactions", sql, StringComparison.Ordinal);
        Assert.Contains("sys.dm_tran_database_transactions", sql, StringComparison.Ordinal);
        Assert.Contains("database_id = DB_ID()", sql, StringComparison.Ordinal);
        Assert.Contains("transaction_state = 2", sql, StringComparison.Ordinal);
        Assert.Contains("SYSDATETIME()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("SYSUTCDATETIME()", sql, StringComparison.Ordinal);
        Assert.Contains("AS ActiveTransactionCount", sql, StringComparison.Ordinal);
        Assert.Contains("AS OldestBeginTime", sql, StringComparison.Ordinal);
        Assert.Contains("AS OldestDurationSeconds", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sys.dm_exec_sessions", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("log_reuse_wait", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRow_ReturnsCount_AndOldestTransaction()
    {
        var begin = new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Unspecified);

        var result = ActiveTransactionsQuery.MapRow(2, begin, 482);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2026, 9, 26, 8, 0, 0, DateTimeKind.Utc), result.OldestBeginTimeUtc);
        Assert.Equal(DateTimeKind.Utc, result.OldestBeginTimeUtc!.Value.Kind);
        Assert.Equal(482, result.OldestDurationSeconds);
    }

    [Fact]
    public void MapRow_WhenCountIsZero_ReturnsNullOldestFields()
    {
        var result = ActiveTransactionsQuery.MapRow(0, null, DBNull.Value);

        Assert.Equal(0, result.Count);
        Assert.Null(result.OldestBeginTimeUtc);
        Assert.Null(result.OldestDurationSeconds);
    }

    [Fact]
    public void MapRow_WhenCountIsZero_Throws_IfOldestFieldsArePresent()
    {
        Assert.Throws<InvalidOperationException>(
            () => ActiveTransactionsQuery.MapRow(
                0,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                null));
        Assert.Throws<InvalidOperationException>(
            () => ActiveTransactionsQuery.MapRow(0, null, 10));
    }

    [Fact]
    public void MapRow_WhenCountIsPositive_Throws_IfOldestFieldsAreNull()
    {
        Assert.Throws<InvalidOperationException>(
            () => ActiveTransactionsQuery.MapRow(1, null, 10));
        Assert.Throws<InvalidOperationException>(
            () => ActiveTransactionsQuery.MapRow(1, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), DBNull.Value));
        Assert.Throws<InvalidOperationException>(
            () => ActiveTransactionsQuery.MapRow(null, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 10));
    }

    [Fact]
    public void MapRow_TreatsUnspecifiedSqlDateTime_AsUtc()
    {
        var fromSql = new DateTime(2026, 9, 21, 12, 16, 24, DateTimeKind.Unspecified);

        var result = ActiveTransactionsQuery.MapRow(1, fromSql, 60);

        Assert.Equal(DateTimeKind.Utc, result.OldestBeginTimeUtc!.Value.Kind);
        Assert.Equal(fromSql.Ticks, result.OldestBeginTimeUtc.Value.Ticks);
    }

    [Fact]
    public void Read_Throws_WhenReaderHasNoRows()
    {
        using var emptyReader = new StubDbDataReader(hasRow: false);

        var ex = Assert.Throws<InvalidOperationException>(() => ActiveTransactionsQuery.Read(emptyReader));

        Assert.Contains("no rows", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetActiveTransactions_ReturnsMetrics_FromConnection()
    {
        var begin = new DateTime(2026, 9, 26, 8, 15, 0, DateTimeKind.Utc);
        using var connection = new StubDbConnection(3, begin, 900);

        var result = _reader.GetActiveTransactions(connection);

        Assert.Equal(3, result.Count);
        Assert.Equal(begin, result.OldestBeginTimeUtc);
        Assert.Equal(DateTimeKind.Utc, result.OldestBeginTimeUtc!.Value.Kind);
        Assert.Equal(900, result.OldestDurationSeconds);
        Assert.Contains("sys.dm_tran_active_transactions", connection.LastCommandText, StringComparison.Ordinal);
        Assert.Contains("DB_ID()", connection.LastCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetActiveTransactions_WhenNoTransactions_ReturnsZeroCount_AndNullOldest()
    {
        using var connection = new StubDbConnection(0, null, null);

        var result = _reader.GetActiveTransactions(connection);

        Assert.Equal(0, result.Count);
        Assert.Null(result.OldestBeginTimeUtc);
        Assert.Null(result.OldestDurationSeconds);
    }

    [Fact]
    public void GetActiveTransactions_UsesProvidedConnection_ToExecuteQuery()
    {
        using var connection = new StubDbConnection(1, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 5);

        _ = _reader.GetActiveTransactions(connection);

        Assert.False(string.IsNullOrWhiteSpace(connection.LastCommandText));
        Assert.Equal(1, connection.CommandsCreated);
    }

    [Fact]
    public void GetActiveTransactions_PropagatesException_FromConnection()
    {
        using var connection = new StubDbConnection(throwOnExecute: true);

        var ex = Assert.Throws<StubDbException>(() => _reader.GetActiveTransactions(connection));

        Assert.Equal("Simulated database failure.", ex.Message);
    }

    [Fact]
    public void GetActiveTransactions_Throws_WhenReaderHasNoRows()
    {
        using var connection = new StubDbConnection(emptyRows: true);

        Assert.Throws<InvalidOperationException>(() => _reader.GetActiveTransactions(connection));
    }

#pragma warning disable CS8765 // Stub ADO.NET overrides; base members use AllowNull setters.
    private sealed class StubDbException : DbException
    {
        public StubDbException(string message)
            : base(message)
        {
        }
    }

    private sealed class StubDbConnection : DbConnection
    {
        private readonly int _count;
        private readonly DateTime? _oldestBegin;
        private readonly int? _oldestDurationSeconds;
        private readonly bool _throwOnExecute;
        private readonly bool _emptyRows;

        public StubDbConnection(int count, DateTime? oldestBegin, int? oldestDurationSeconds)
        {
            _count = count;
            _oldestBegin = oldestBegin;
            _oldestDurationSeconds = oldestDurationSeconds;
        }

        public StubDbConnection(bool throwOnExecute = false, bool emptyRows = false)
        {
            _throwOnExecute = throwOnExecute;
            _emptyRows = emptyRows;
        }

        public string LastCommandText { get; private set; } = string.Empty;
        public int CommandsCreated { get; private set; }

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "stub";
        public override string DataSource => "stub";
        public override string ServerVersion => "stub";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
        {
            CommandsCreated++;
            return new StubDbCommand(
                this,
                _count,
                _oldestBegin,
                _oldestDurationSeconds,
                _throwOnExecute,
                _emptyRows);
        }

        private void CaptureCommandText(string commandText) => LastCommandText = commandText;

        private sealed class StubDbCommand : DbCommand
        {
            private readonly StubDbConnection _connection;
            private readonly int _count;
            private readonly DateTime? _oldestBegin;
            private readonly int? _oldestDurationSeconds;
            private readonly bool _throwOnExecute;
            private readonly bool _emptyRows;
            private string _commandText = string.Empty;

            public StubDbCommand(
                StubDbConnection connection,
                int count,
                DateTime? oldestBegin,
                int? oldestDurationSeconds,
                bool throwOnExecute,
                bool emptyRows)
            {
                _connection = connection;
                _count = count;
                _oldestBegin = oldestBegin;
                _oldestDurationSeconds = oldestDurationSeconds;
                _throwOnExecute = throwOnExecute;
                _emptyRows = emptyRows;
            }

            public override string CommandText
            {
                get => _commandText;
                set
                {
                    _commandText = value ?? string.Empty;
                    _connection.CaptureCommandText(_commandText);
                }
            }

            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; } = CommandType.Text;
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection? DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection { get; } = new StubParameterCollection();
            protected override DbTransaction? DbTransaction { get; set; }

            public override void Cancel() { }
            public override int ExecuteNonQuery() => 0;
            public override object? ExecuteScalar() => throw new NotSupportedException();
            public override void Prepare() { }

            protected override DbParameter CreateDbParameter() => new StubDbParameter();

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            {
                if (_throwOnExecute)
                {
                    throw new StubDbException("Simulated database failure.");
                }

                return new StubDbDataReader(
                    hasRow: !_emptyRows,
                    _count,
                    _oldestBegin,
                    _oldestDurationSeconds);
            }
        }

        private sealed class StubDbParameter : DbParameter
        {
            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; }
            public override bool IsNullable { get; set; }
            public override string ParameterName { get; set; } = string.Empty;
            public override string SourceColumn { get; set; } = string.Empty;
            public override object? Value { get; set; }
            public override bool SourceColumnNullMapping { get; set; }
            public override int Size { get; set; }
            public override void ResetDbType() { }
        }

        private sealed class StubParameterCollection : DbParameterCollection
        {
            private readonly List<object> _items = [];

            public override int Count => _items.Count;
            public override object SyncRoot => _items;

            public override int Add(object value)
            {
                _items.Add(value);
                return _items.Count - 1;
            }

            public override void AddRange(Array values) => _items.AddRange(values.Cast<object>());
            public override void Clear() => _items.Clear();
            public override bool Contains(object value) => _items.Contains(value);
            public override bool Contains(string value) => false;
            public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);
            public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
            public override int IndexOf(object value) => _items.IndexOf(value);
            public override int IndexOf(string parameterName) => -1;
            public override void Insert(int index, object value) => _items.Insert(index, value);
            public override void Remove(object value) => _items.Remove(value);
            public override void RemoveAt(int index) => _items.RemoveAt(index);
            public override void RemoveAt(string parameterName) { }
            protected override DbParameter GetParameter(int index) => (DbParameter)_items[index];
            protected override DbParameter GetParameter(string parameterName) =>
                throw new NotSupportedException();
            protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
            protected override void SetParameter(string parameterName, DbParameter value) { }
        }
    }

    private sealed class StubDbDataReader : DbDataReader
    {
        private readonly bool _hasRow;
        private readonly object?[] _values;
        private int _rowIndex = -1;

        public StubDbDataReader(bool hasRow)
            : this(hasRow, 0, null, null)
        {
        }

        public StubDbDataReader(bool hasRow, int count, DateTime? oldestBegin, int? oldestDurationSeconds)
        {
            _hasRow = hasRow;
            _values =
            [
                count,
                oldestBegin.HasValue ? oldestBegin.Value : DBNull.Value,
                oldestDurationSeconds.HasValue ? oldestDurationSeconds.Value : DBNull.Value
            ];
        }

        public override int Depth => 0;
        public override int FieldCount => 3;
        public override bool HasRows => _hasRow;
        public override bool IsClosed => false;
        public override int RecordsAffected => -1;

        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => throw new NotSupportedException();

        public override bool Read()
        {
            if (!_hasRow)
            {
                return false;
            }

            _rowIndex++;
            return _rowIndex == 0;
        }

        public override bool NextResult() => false;
        public override bool IsDBNull(int ordinal) => _values[ordinal] is null or DBNull;
        public override object GetValue(int ordinal) => _values[ordinal]!;
        public override int GetValues(object[] values)
        {
            Array.Copy(_values, values, _values.Length);
            return _values.Length;
        }

        public override string GetName(int ordinal) => ordinal switch
        {
            0 => "ActiveTransactionCount",
            1 => "OldestBeginTime",
            2 => "OldestDurationSeconds",
            _ => throw new IndexOutOfRangeException()
        };

        public override int GetOrdinal(string name) => name switch
        {
            "ActiveTransactionCount" => 0,
            "OldestBeginTime" => 1,
            "OldestDurationSeconds" => 2,
            _ => throw new IndexOutOfRangeException()
        };

        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

        public override Type GetFieldType(int ordinal) => ordinal switch
        {
            0 => typeof(int),
            1 => typeof(DateTime),
            2 => typeof(int),
            _ => throw new IndexOutOfRangeException()
        };

        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => Convert.ToInt32(_values[ordinal]);
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override string GetString(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(_values[ordinal]);
        public override System.Collections.IEnumerator GetEnumerator() => _values.GetEnumerator();
    }
#pragma warning restore CS8765
}
