using System.Data;
using System.Data.Common;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Tests;

public class LogSpaceReaderTests
{
    private readonly LogSpaceReader _reader = new();

    [Fact]
    public void Build_SelectsLogSpace_FromDmDbLogSpaceUsage()
    {
        var sql = LogSpaceQuery.Build();

        Assert.Contains("sys.dm_db_log_space_usage", sql, StringComparison.Ordinal);
        Assert.Contains("AS TotalLogMB", sql, StringComparison.Ordinal);
        Assert.Contains("AS UsedLogMB", sql, StringComparison.Ordinal);
        Assert.Contains("AS FreeLogMB", sql, StringComparison.Ordinal);
        Assert.Contains("AS UsedPercent", sql, StringComparison.Ordinal);
        Assert.Contains("decimal(18, 2)", sql, StringComparison.Ordinal);
        Assert.Contains("1048576.0", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sys.dm_db_file_space_usage", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("log_reuse_wait", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRow_ReturnsDecimals_WhenValuesPresent()
    {
        var result = LogSpaceQuery.MapRow(1024.50m, 845.25m, 179.25m, 82.50m);

        Assert.Equal(1024.50m, result.TotalLogMB);
        Assert.Equal(845.25m, result.UsedLogMB);
        Assert.Equal(179.25m, result.FreeLogMB);
        Assert.Equal(82.50m, result.UsedPercent);
    }

    [Fact]
    public void MapRow_ConvertsNumericObjects_ToDecimal()
    {
        var result = LogSpaceQuery.MapRow(1024.0d, 512, 512L, 50.0f);

        Assert.Equal(1024.0m, result.TotalLogMB);
        Assert.Equal(512m, result.UsedLogMB);
        Assert.Equal(512m, result.FreeLogMB);
        Assert.Equal(50.0m, result.UsedPercent);
    }

    [Fact]
    public void MapRow_Throws_WhenAnyValueIsNull()
    {
        Assert.Throws<InvalidOperationException>(
            () => LogSpaceQuery.MapRow(null, 1m, 1m, 1m));
        Assert.Throws<InvalidOperationException>(
            () => LogSpaceQuery.MapRow(1m, DBNull.Value, 1m, 1m));
        Assert.Throws<InvalidOperationException>(
            () => LogSpaceQuery.MapRow(1m, 1m, null, 1m));
        Assert.Throws<InvalidOperationException>(
            () => LogSpaceQuery.MapRow(1m, 1m, 1m, DBNull.Value));
    }

    [Fact]
    public void Read_Throws_WhenReaderHasNoRows()
    {
        using var emptyReader = new StubDbDataReader(hasRow: false);

        var ex = Assert.Throws<InvalidOperationException>(() => LogSpaceQuery.Read(emptyReader));

        Assert.Contains("no rows", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetLogSpace_ReturnsMetrics_FromConnection()
    {
        using var connection = new StubDbConnection(1024.50m, 845.25m, 179.25m, 82.50m);

        var result = _reader.GetLogSpace(connection);

        Assert.Equal(1024.50m, result.TotalLogMB);
        Assert.Equal(845.25m, result.UsedLogMB);
        Assert.Equal(179.25m, result.FreeLogMB);
        Assert.Equal(82.50m, result.UsedPercent);
        Assert.Contains("sys.dm_db_log_space_usage", connection.LastCommandText, StringComparison.Ordinal);
        Assert.Contains("AS UsedPercent", connection.LastCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetLogSpace_UsesProvidedConnection_ToExecuteQuery()
    {
        using var connection = new StubDbConnection(100m, 40m, 60m, 40m);

        _ = _reader.GetLogSpace(connection);

        Assert.False(string.IsNullOrWhiteSpace(connection.LastCommandText));
        Assert.Equal(1, connection.CommandsCreated);
    }

    [Fact]
    public void GetLogSpace_PropagatesException_FromConnection()
    {
        using var connection = new StubDbConnection(throwOnExecute: true);

        var ex = Assert.Throws<StubDbException>(() => _reader.GetLogSpace(connection));

        Assert.Equal("Simulated database failure.", ex.Message);
    }

    [Fact]
    public void GetLogSpace_Throws_WhenReaderHasNoRows()
    {
        using var connection = new StubDbConnection(emptyRows: true);

        Assert.Throws<InvalidOperationException>(() => _reader.GetLogSpace(connection));
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
        private readonly decimal _totalLogMb;
        private readonly decimal _usedLogMb;
        private readonly decimal _freeLogMb;
        private readonly decimal _usedPercent;
        private readonly bool _throwOnExecute;
        private readonly bool _emptyRows;

        public StubDbConnection(
            decimal totalLogMb,
            decimal usedLogMb,
            decimal freeLogMb,
            decimal usedPercent)
        {
            _totalLogMb = totalLogMb;
            _usedLogMb = usedLogMb;
            _freeLogMb = freeLogMb;
            _usedPercent = usedPercent;
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
                _totalLogMb,
                _usedLogMb,
                _freeLogMb,
                _usedPercent,
                _throwOnExecute,
                _emptyRows);
        }

        private void CaptureCommandText(string commandText) => LastCommandText = commandText;

        private sealed class StubDbCommand : DbCommand
        {
            private readonly StubDbConnection _connection;
            private readonly decimal _totalLogMb;
            private readonly decimal _usedLogMb;
            private readonly decimal _freeLogMb;
            private readonly decimal _usedPercent;
            private readonly bool _throwOnExecute;
            private readonly bool _emptyRows;
            private string _commandText = string.Empty;

            public StubDbCommand(
                StubDbConnection connection,
                decimal totalLogMb,
                decimal usedLogMb,
                decimal freeLogMb,
                decimal usedPercent,
                bool throwOnExecute,
                bool emptyRows)
            {
                _connection = connection;
                _totalLogMb = totalLogMb;
                _usedLogMb = usedLogMb;
                _freeLogMb = freeLogMb;
                _usedPercent = usedPercent;
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
                    _totalLogMb,
                    _usedLogMb,
                    _freeLogMb,
                    _usedPercent);
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
            : this(hasRow, 0m, 0m, 0m, 0m)
        {
        }

        public StubDbDataReader(
            bool hasRow,
            decimal totalLogMb,
            decimal usedLogMb,
            decimal freeLogMb,
            decimal usedPercent)
        {
            _hasRow = hasRow;
            _values = [totalLogMb, usedLogMb, freeLogMb, usedPercent];
        }

        public override int Depth => 0;
        public override int FieldCount => 4;
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
            0 => "TotalLogMB",
            1 => "UsedLogMB",
            2 => "FreeLogMB",
            3 => "UsedPercent",
            _ => throw new IndexOutOfRangeException()
        };

        public override int GetOrdinal(string name) => name switch
        {
            "TotalLogMB" => 0,
            "UsedLogMB" => 1,
            "FreeLogMB" => 2,
            "UsedPercent" => 3,
            _ => throw new IndexOutOfRangeException()
        };

        public override string GetDataTypeName(int ordinal) => typeof(decimal).Name;
        public override Type GetFieldType(int ordinal) => typeof(decimal);
        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => Convert.ToDouble(_values[ordinal]);
        public override string GetString(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => (decimal)_values[ordinal];
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override System.Collections.IEnumerator GetEnumerator() => _values.GetEnumerator();
    }
#pragma warning restore CS8765
}
