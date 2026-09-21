using System.Data;
using System.Data.Common;
using Hangfire.Monitor.Infrastructure.Storage;
using Hangfire.States;

namespace Hangfire.Monitor.Tests;

public class LastFailedAtReaderTests
{
    private readonly LastFailedAtReader _reader = new();

    [Fact]
    public void ReadScalar_ReturnsDateTime_WhenTimestampPresent()
    {
        var failedAt = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);

        var result = LastFailedAtQuery.ReadScalar(failedAt);

        Assert.Equal(failedAt, result);
        Assert.Equal(DateTimeKind.Utc, result!.Value.Kind);
    }

    [Fact]
    public void ReadScalar_TreatsUnspecifiedSqlDateTime_AsUtc()
    {
        // ADO.NET surfaces SQL datetime as Unspecified; Hangfire wrote UtcNow.
        var fromSql = new DateTime(2026, 9, 21, 12, 16, 24, DateTimeKind.Unspecified);

        var result = LastFailedAtQuery.ReadScalar(fromSql);

        Assert.NotNull(result);
        Assert.Equal(DateTimeKind.Utc, result.Value.Kind);
        Assert.Equal(fromSql.Ticks, result.Value.Ticks);
        Assert.Equal(new DateTime(2026, 9, 21, 12, 16, 24, DateTimeKind.Utc), result.Value);
    }

    [Fact]
    public void ReadScalar_ReturnsNull_WhenSqlReturnsNull()
    {
        Assert.Null(LastFailedAtQuery.ReadScalar(null));
        Assert.Null(LastFailedAtQuery.ReadScalar(DBNull.Value));
    }

    [Fact]
    public void Build_UsesConfiguredSchema_InBracketedIdentifiers()
    {
        var sql = LastFailedAtQuery.Build("CustomHangfire");

        Assert.Contains("[CustomHangfire].[Job]", sql, StringComparison.Ordinal);
        Assert.Contains("[CustomHangfire].[State]", sql, StringComparison.Ordinal);
        Assert.Contains($"N'{FailedState.StateName}'", sql, StringComparison.Ordinal);
        Assert.Contains("MAX(s.[CreatedAt])", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[HangFire]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EscapesClosingBracket_InSchemaName()
    {
        var sql = LastFailedAtQuery.Build("My]Schema");

        Assert.Contains("[My]]Schema].[Job]", sql, StringComparison.Ordinal);
        Assert.Contains("[My]]Schema].[State]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void GetLastFailedAt_ReturnsTimestamp_FromConnection()
    {
        var failedAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        using var connection = new StubDbConnection(failedAt);

        var result = _reader.GetLastFailedAt(connection, "HangFire");

        Assert.Equal(failedAt, result);
        Assert.Equal(DateTimeKind.Utc, result!.Value.Kind);
        Assert.Contains("[HangFire].[Job]", connection.LastCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetLastFailedAt_ReturnsNull_WhenConnectionScalarIsNull()
    {
        using var connection = new StubDbConnection(DBNull.Value);

        var result = _reader.GetLastFailedAt(connection, "HangFire");

        Assert.Null(result);
    }

    [Fact]
    public void GetLastFailedAt_UsesSchemaArgument_InExecutedSql()
    {
        using var connection = new StubDbConnection(DBNull.Value);

        _ = _reader.GetLastFailedAt(connection, "AppSchema");

        Assert.Contains("[AppSchema].[Job]", connection.LastCommandText, StringComparison.Ordinal);
        Assert.Contains("[AppSchema].[State]", connection.LastCommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("[HangFire]", connection.LastCommandText, StringComparison.Ordinal);
    }

#pragma warning disable CS8765 // Stub ADO.NET overrides; base members use AllowNull setters.
    private sealed class StubDbConnection : DbConnection
    {
        private readonly object? _scalar;

        public StubDbConnection(object? scalar)
        {
            _scalar = scalar;
        }

        public string LastCommandText { get; private set; } = string.Empty;

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

        protected override DbCommand CreateDbCommand() => new StubDbCommand(this, _scalar);

        private void CaptureCommandText(string commandText) => LastCommandText = commandText;

        private sealed class StubDbCommand : DbCommand
        {
            private readonly StubDbConnection _connection;
            private readonly object? _scalar;
            private string _commandText = string.Empty;

            public StubDbCommand(StubDbConnection connection, object? scalar)
            {
                _connection = connection;
                _scalar = scalar;
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
            public override object? ExecuteScalar() => _scalar;
            public override void Prepare() { }

            protected override DbParameter CreateDbParameter() => new StubDbParameter();
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
                throw new NotSupportedException();
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
