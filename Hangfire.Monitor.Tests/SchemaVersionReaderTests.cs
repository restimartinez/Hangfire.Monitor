using System.Data;
using System.Data.Common;
using Hangfire.Monitor.Infrastructure.Storage;

namespace Hangfire.Monitor.Tests;

public class SchemaVersionReaderTests
{
    private readonly SchemaVersionReader _reader = new();

    [Fact]
    public void Build_UsesDefaultHangFireSchema_InBracketedIdentifiers()
    {
        var sql = SchemaVersionQuery.Build("HangFire");

        Assert.Contains("[HangFire].[Schema]", sql, StringComparison.Ordinal);
        Assert.Contains("SELECT [Version]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UsesConfiguredSchema_InBracketedIdentifiers()
    {
        var sql = SchemaVersionQuery.Build("CustomHangfire");

        Assert.Contains("[CustomHangfire].[Schema]", sql, StringComparison.Ordinal);
        Assert.Contains("SELECT [Version]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("[HangFire]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EscapesClosingBracket_InSchemaName()
    {
        var sql = SchemaVersionQuery.Build("My]Schema");

        Assert.Contains("[My]]Schema].[Schema]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadScalar_ReturnsInt_WhenVersionPresent()
    {
        var result = SchemaVersionQuery.ReadScalar(9);

        Assert.Equal(9, result);
    }

    [Fact]
    public void ReadScalar_ReturnsNull_WhenValueIsNull()
    {
        Assert.Null(SchemaVersionQuery.ReadScalar(null));
    }

    [Fact]
    public void ReadScalar_ReturnsNull_WhenValueIsDbNull()
    {
        Assert.Null(SchemaVersionQuery.ReadScalar(DBNull.Value));
    }

    [Fact]
    public void GetSchemaVersion_ReturnsVersion_FromConnection()
    {
        using var connection = new StubDbConnection(9);

        var result = _reader.GetSchemaVersion(connection, "HangFire");

        Assert.Equal(9, result);
        Assert.Contains("[HangFire].[Schema]", connection.LastCommandText, StringComparison.Ordinal);
        Assert.Contains("SELECT [Version]", connection.LastCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSchemaVersion_UsesSchemaArgument_InExecutedSql()
    {
        using var connection = new StubDbConnection(9);

        _ = _reader.GetSchemaVersion(connection, "AppSchema");

        Assert.Contains("[AppSchema].[Schema]", connection.LastCommandText, StringComparison.Ordinal);
        Assert.DoesNotContain("[HangFire]", connection.LastCommandText, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSchemaVersion_PropagatesException_FromConnection()
    {
        using var connection = new StubDbConnection(throwOnExecute: true);

        var ex = Assert.Throws<StubDbException>(
            () => _reader.GetSchemaVersion(connection, "HangFire"));

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
        private readonly object? _scalar;
        private readonly bool _throwOnExecute;

        public StubDbConnection(object? scalar)
        {
            _scalar = scalar;
        }

        public StubDbConnection(bool throwOnExecute)
        {
            _throwOnExecute = throwOnExecute;
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

        protected override DbCommand CreateDbCommand() =>
            new StubDbCommand(this, _scalar, _throwOnExecute);

        private void CaptureCommandText(string commandText) => LastCommandText = commandText;

        private sealed class StubDbCommand : DbCommand
        {
            private readonly StubDbConnection _connection;
            private readonly object? _scalar;
            private readonly bool _throwOnExecute;
            private string _commandText = string.Empty;

            public StubDbCommand(StubDbConnection connection, object? scalar, bool throwOnExecute)
            {
                _connection = connection;
                _scalar = scalar;
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

            public override object? ExecuteScalar()
            {
                if (_throwOnExecute)
                {
                    throw new StubDbException("Simulated database failure.");
                }

                return _scalar;
            }

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
