using System.Data;
using System.Data.Common;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Tests;

public class LogReuseWaitReaderTests
{
    private readonly LogReuseWaitReader _reader = new();

    [Fact]
    public void Build_SelectsLogReuseWait_FromSysDatabases_ForCurrentDatabase()
    {
        var sql = LogReuseWaitQuery.Build();

        Assert.Contains("sys.databases", sql, StringComparison.Ordinal);
        Assert.Contains("log_reuse_wait", sql, StringComparison.Ordinal);
        Assert.Contains("log_reuse_wait_desc", sql, StringComparison.Ordinal);
        Assert.Contains("recovery_model_desc", sql, StringComparison.Ordinal);
        Assert.Contains("database_id = DB_ID()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sys.dm_db_log_space_usage", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sys.dm_tran_active_transactions", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRow_ReturnsValues_WhenPresent()
    {
        var result = LogReuseWaitQuery.MapRow(0, "NOTHING", "FULL");

        Assert.Equal(0, result.Wait);
        Assert.Equal("NOTHING", result.WaitDescription);
        Assert.Equal("FULL", result.RecoveryModel);
    }

    [Fact]
    public void MapRow_PreservesLogBackup_AsData()
    {
        var result = LogReuseWaitQuery.MapRow(2, "LOG_BACKUP", "FULL");

        Assert.Equal(2, result.Wait);
        Assert.Equal("LOG_BACKUP", result.WaitDescription);
        Assert.Equal("FULL", result.RecoveryModel);
    }

    [Fact]
    public void MapRow_PreservesActiveTransaction_AsData()
    {
        var result = LogReuseWaitQuery.MapRow(4, "ACTIVE_TRANSACTION", "FULL");

        Assert.Equal(4, result.Wait);
        Assert.Equal("ACTIVE_TRANSACTION", result.WaitDescription);
        Assert.Equal("FULL", result.RecoveryModel);
    }

    [Fact]
    public void MapRow_Throws_WhenAnyValueIsNull()
    {
        Assert.Throws<InvalidOperationException>(
            () => LogReuseWaitQuery.MapRow(null, "NOTHING", "FULL"));
        Assert.Throws<InvalidOperationException>(
            () => LogReuseWaitQuery.MapRow(0, DBNull.Value, "FULL"));
        Assert.Throws<InvalidOperationException>(
            () => LogReuseWaitQuery.MapRow(0, "NOTHING", null));
    }

    [Fact]
    public void Read_Throws_WhenReaderHasNoRows()
    {
        using var emptyReader = new StubDbDataReader(hasRow: false);

        var ex = Assert.Throws<InvalidOperationException>(() => LogReuseWaitQuery.Read(emptyReader));

        Assert.Contains("no rows", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetLogReuseWait_ReturnsMetrics_FromConnection()
    {
        using var connection = new StubDbConnection(0, "NOTHING", "SIMPLE");

        var result = _reader.GetLogReuseWait(connection);

        Assert.Equal(0, result.Wait);
        Assert.Equal("NOTHING", result.WaitDescription);
        Assert.Equal("SIMPLE", result.RecoveryModel);
        Assert.Contains("sys.databases", connection.LastCommandText, StringComparison.Ordinal);
        Assert.Contains("log_reuse_wait_desc", connection.LastCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetLogReuseWait_UsesProvidedConnection_ToExecuteQuery()
    {
        using var connection = new StubDbConnection(2, "LOG_BACKUP", "FULL");

        _ = _reader.GetLogReuseWait(connection);

        Assert.False(string.IsNullOrWhiteSpace(connection.LastCommandText));
        Assert.Equal(1, connection.CommandsCreated);
    }

    [Fact]
    public void GetLogReuseWait_PropagatesException_FromConnection()
    {
        using var connection = new StubDbConnection(throwOnExecute: true);

        var ex = Assert.Throws<StubDbException>(() => _reader.GetLogReuseWait(connection));

        Assert.Equal("Simulated database failure.", ex.Message);
    }

    [Fact]
    public void GetLogReuseWait_Throws_WhenReaderHasNoRows()
    {
        using var connection = new StubDbConnection(emptyRows: true);

        Assert.Throws<InvalidOperationException>(() => _reader.GetLogReuseWait(connection));
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
        private readonly int _wait;
        private readonly string _waitDescription;
        private readonly string _recoveryModel;
        private readonly bool _throwOnExecute;
        private readonly bool _emptyRows;

        public StubDbConnection(int wait, string waitDescription, string recoveryModel)
        {
            _wait = wait;
            _waitDescription = waitDescription;
            _recoveryModel = recoveryModel;
        }

        public StubDbConnection(bool throwOnExecute = false, bool emptyRows = false)
        {
            _throwOnExecute = throwOnExecute;
            _emptyRows = emptyRows;
            _waitDescription = string.Empty;
            _recoveryModel = string.Empty;
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
                _wait,
                _waitDescription,
                _recoveryModel,
                _throwOnExecute,
                _emptyRows);
        }

        private void CaptureCommandText(string commandText) => LastCommandText = commandText;

        private sealed class StubDbCommand : DbCommand
        {
            private readonly StubDbConnection _connection;
            private readonly int _wait;
            private readonly string _waitDescription;
            private readonly string _recoveryModel;
            private readonly bool _throwOnExecute;
            private readonly bool _emptyRows;
            private string _commandText = string.Empty;

            public StubDbCommand(
                StubDbConnection connection,
                int wait,
                string waitDescription,
                string recoveryModel,
                bool throwOnExecute,
                bool emptyRows)
            {
                _connection = connection;
                _wait = wait;
                _waitDescription = waitDescription;
                _recoveryModel = recoveryModel;
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
                    _wait,
                    _waitDescription,
                    _recoveryModel);
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
        private readonly object[] _values;
        private int _rowIndex = -1;

        public StubDbDataReader(bool hasRow)
            : this(hasRow, 0, string.Empty, string.Empty)
        {
        }

        public StubDbDataReader(bool hasRow, int wait, string waitDescription, string recoveryModel)
        {
            _hasRow = hasRow;
            _values = [wait, waitDescription, recoveryModel];
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
        public override bool IsDBNull(int ordinal) => false;
        public override object GetValue(int ordinal) => _values[ordinal];
        public override int GetValues(object[] values)
        {
            _values.CopyTo(values, 0);
            return _values.Length;
        }

        public override string GetName(int ordinal) => ordinal switch
        {
            0 => "log_reuse_wait",
            1 => "log_reuse_wait_desc",
            2 => "recovery_model_desc",
            _ => throw new IndexOutOfRangeException()
        };

        public override int GetOrdinal(string name) => name switch
        {
            "log_reuse_wait" => 0,
            "log_reuse_wait_desc" => 1,
            "recovery_model_desc" => 2,
            _ => throw new IndexOutOfRangeException()
        };

        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
        public override Type GetFieldType(int ordinal) => ordinal == 0 ? typeof(int) : typeof(string);
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => (int)_values[ordinal];
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override string GetString(int ordinal) => (string)_values[ordinal];
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override System.Collections.IEnumerator GetEnumerator() => _values.GetEnumerator();
    }
#pragma warning restore CS8765
}
