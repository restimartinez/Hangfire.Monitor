using System.Data;
using System.Data.Common;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Tests;

public class DataFileSpaceReaderTests
{
    private readonly DataFileSpaceReader _reader = new();

    [Fact]
    public void Build_SelectsAggregatedPageCounts_FromDmDbFileSpaceUsage()
    {
        var sql = DataFileSpaceQuery.Build();

        Assert.Contains("sys.dm_db_file_space_usage", sql, StringComparison.Ordinal);
        Assert.Contains("SUM(total_page_count) / 128.0", sql, StringComparison.Ordinal);
        Assert.Contains("SUM(allocated_extent_page_count) / 128.0", sql, StringComparison.Ordinal);
        Assert.Contains("SUM(unallocated_extent_page_count) / 128.0", sql, StringComparison.Ordinal);
        Assert.Contains("AS AllocatedMB", sql, StringComparison.Ordinal);
        Assert.Contains("AS UsedMB", sql, StringComparison.Ordinal);
        Assert.Contains("AS FreeMB", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRow_ReturnsDecimals_WhenValuesPresent()
    {
        var result = DataFileSpaceQuery.MapRow(2048.5m, 1350.25m, 698.25m);

        Assert.Equal(2048.5m, result.AllocatedMB);
        Assert.Equal(1350.25m, result.UsedMB);
        Assert.Equal(698.25m, result.FreeMB);
    }

    [Fact]
    public void MapRow_ConvertsNumericObjects_ToDecimal()
    {
        var result = DataFileSpaceQuery.MapRow(1024.0d, 512, 512L);

        Assert.Equal(1024.0m, result.AllocatedMB);
        Assert.Equal(512m, result.UsedMB);
        Assert.Equal(512m, result.FreeMB);
    }

    [Fact]
    public void MapRow_Throws_WhenAnyValueIsNull()
    {
        Assert.Throws<InvalidOperationException>(
            () => DataFileSpaceQuery.MapRow(null, 1m, 1m));
        Assert.Throws<InvalidOperationException>(
            () => DataFileSpaceQuery.MapRow(1m, DBNull.Value, 1m));
        Assert.Throws<InvalidOperationException>(
            () => DataFileSpaceQuery.MapRow(1m, 1m, null));
    }

    [Fact]
    public void GetDataFileSpace_ReturnsMetrics_FromConnection()
    {
        using var connection = new StubDbConnection(2048.5m, 1350.25m, 698.25m);

        var result = _reader.GetDataFileSpace(connection);

        Assert.Equal(2048.5m, result.AllocatedMB);
        Assert.Equal(1350.25m, result.UsedMB);
        Assert.Equal(698.25m, result.FreeMB);
        Assert.Contains("sys.dm_db_file_space_usage", connection.LastCommandText, StringComparison.Ordinal);
        Assert.Contains("AS AllocatedMB", connection.LastCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDataFileSpace_UsesProvidedConnection_ToExecuteQuery()
    {
        using var connection = new StubDbConnection(100m, 40m, 60m);

        _ = _reader.GetDataFileSpace(connection);

        Assert.False(string.IsNullOrWhiteSpace(connection.LastCommandText));
        Assert.Equal(1, connection.CommandsCreated);
    }

    [Fact]
    public void GetDataFileSpace_PropagatesException_FromConnection()
    {
        using var connection = new StubDbConnection(throwOnExecute: true);

        var ex = Assert.Throws<StubDbException>(() => _reader.GetDataFileSpace(connection));

        Assert.Equal("Simulated database failure.", ex.Message);
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
        private readonly decimal _allocatedMb;
        private readonly decimal _usedMb;
        private readonly decimal _freeMb;
        private readonly bool _throwOnExecute;

        public StubDbConnection(decimal allocatedMb, decimal usedMb, decimal freeMb)
        {
            _allocatedMb = allocatedMb;
            _usedMb = usedMb;
            _freeMb = freeMb;
        }

        public StubDbConnection(bool throwOnExecute)
        {
            _throwOnExecute = throwOnExecute;
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
            return new StubDbCommand(this, _allocatedMb, _usedMb, _freeMb, _throwOnExecute);
        }

        private void CaptureCommandText(string commandText) => LastCommandText = commandText;

        private sealed class StubDbCommand : DbCommand
        {
            private readonly StubDbConnection _connection;
            private readonly decimal _allocatedMb;
            private readonly decimal _usedMb;
            private readonly decimal _freeMb;
            private readonly bool _throwOnExecute;
            private string _commandText = string.Empty;

            public StubDbCommand(
                StubDbConnection connection,
                decimal allocatedMb,
                decimal usedMb,
                decimal freeMb,
                bool throwOnExecute)
            {
                _connection = connection;
                _allocatedMb = allocatedMb;
                _usedMb = usedMb;
                _freeMb = freeMb;
                _throwOnExecute = throwOnExecute;
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

                return new StubDbDataReader(_allocatedMb, _usedMb, _freeMb);
            }
        }

        private sealed class StubDbDataReader : DbDataReader
        {
            private readonly object[] _values;
            private int _rowIndex = -1;

            public StubDbDataReader(decimal allocatedMb, decimal usedMb, decimal freeMb)
            {
                _values = [allocatedMb, usedMb, freeMb];
            }

            public override int Depth => 0;
            public override int FieldCount => 3;
            public override bool HasRows => true;
            public override bool IsClosed => false;
            public override int RecordsAffected => -1;

            public override object this[int ordinal] => GetValue(ordinal);
            public override object this[string name] => throw new NotSupportedException();

            public override bool Read()
            {
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
                0 => "AllocatedMB",
                1 => "UsedMB",
                2 => "FreeMB",
                _ => throw new IndexOutOfRangeException()
            };

            public override int GetOrdinal(string name) => name switch
            {
                "AllocatedMB" => 0,
                "UsedMB" => 1,
                "FreeMB" => 2,
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
#pragma warning restore CS8765
}
