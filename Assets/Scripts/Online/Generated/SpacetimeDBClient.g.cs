
#nullable enable

using System;
using SpacetimeDB.ClientApi;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SpacetimeDB.Types
{
    public sealed partial class RemoteReducers : RemoteBase
    {
        internal RemoteReducers(DbConnection conn) : base(conn) { }
        internal event Action<ReducerEventContext, Exception>? InternalOnUnhandledReducerError;
    }

    public sealed partial class RemoteProcedures : RemoteBase
    {
        internal RemoteProcedures(DbConnection conn) : base(conn) { }
    }

    public sealed partial class RemoteTables : RemoteTablesBase
    {
        public RemoteTables(DbConnection conn)
        {
            AddTable(MatchResult = new(conn));
            AddTable(Pairing = new(conn));
            AddTable(Player = new(conn));
            AddTable(QueueEntry = new(conn));
        }
    }

    public interface IRemoteDbContext : IDbContext<RemoteTables, RemoteReducers, SubscriptionBuilder, RemoteProcedures>
    {
        public event Action<ReducerEventContext, Exception>? OnUnhandledReducerError;
    }

    public sealed class EventContext : IEventContext, IRemoteDbContext
    {
        private readonly DbConnection conn;

        public readonly Event<Reducer> Event;

        public RemoteTables Db => conn.Db;
        public RemoteReducers Reducers => conn.Reducers;
        public RemoteProcedures Procedures => conn.Procedures;
        public bool IsActive => conn.IsActive;
        public void Disconnect()
        {
            conn.Disconnect();
        }
        public SubscriptionBuilder SubscriptionBuilder() => conn.SubscriptionBuilder();
        public Identity? Identity => conn.Identity;
        public ConnectionId ConnectionId => conn.ConnectionId;
        public event Action<ReducerEventContext, Exception>? OnUnhandledReducerError
        {
            add => Reducers.InternalOnUnhandledReducerError += value;
            remove => Reducers.InternalOnUnhandledReducerError -= value;
        }

        internal EventContext(DbConnection conn, Event<Reducer> Event)
        {
            this.conn = conn;
            this.Event = Event;
        }
    }

    public sealed class ReducerEventContext : IReducerEventContext, IRemoteDbContext
    {
        private readonly DbConnection conn;
        public readonly ReducerEvent<Reducer> Event;

        public RemoteTables Db => conn.Db;
        public RemoteReducers Reducers => conn.Reducers;
        public RemoteProcedures Procedures => conn.Procedures;
        public bool IsActive => conn.IsActive;
        public void Disconnect()
        {
            conn.Disconnect();
        }
        public SubscriptionBuilder SubscriptionBuilder() => conn.SubscriptionBuilder();
        public Identity? Identity => conn.Identity;
        public ConnectionId ConnectionId => conn.ConnectionId;
        public event Action<ReducerEventContext, Exception>? OnUnhandledReducerError
        {
            add => Reducers.InternalOnUnhandledReducerError += value;
            remove => Reducers.InternalOnUnhandledReducerError -= value;
        }

        internal ReducerEventContext(DbConnection conn, ReducerEvent<Reducer> reducerEvent)
        {
            this.conn = conn;
            Event = reducerEvent;
        }
    }

    public sealed class ErrorContext : IErrorContext, IRemoteDbContext
    {
        private readonly DbConnection conn;
        public readonly Exception Event;
        Exception IErrorContext.Event
        {
            get
            {
                return Event;
            }
        }

        public RemoteTables Db => conn.Db;
        public RemoteReducers Reducers => conn.Reducers;
        public RemoteProcedures Procedures => conn.Procedures;
        public bool IsActive => conn.IsActive;
        public void Disconnect()
        {
            conn.Disconnect();
        }
        public SubscriptionBuilder SubscriptionBuilder() => conn.SubscriptionBuilder();
        public Identity? Identity => conn.Identity;
        public ConnectionId ConnectionId => conn.ConnectionId;
        public event Action<ReducerEventContext, Exception>? OnUnhandledReducerError
        {
            add => Reducers.InternalOnUnhandledReducerError += value;
            remove => Reducers.InternalOnUnhandledReducerError -= value;
        }

        internal ErrorContext(DbConnection conn, Exception error)
        {
            this.conn = conn;
            Event = error;
        }
    }

    public sealed class SubscriptionEventContext : ISubscriptionEventContext, IRemoteDbContext
    {
        private readonly DbConnection conn;

        public RemoteTables Db => conn.Db;
        public RemoteReducers Reducers => conn.Reducers;
        public RemoteProcedures Procedures => conn.Procedures;
        public bool IsActive => conn.IsActive;
        public void Disconnect()
        {
            conn.Disconnect();
        }
        public SubscriptionBuilder SubscriptionBuilder() => conn.SubscriptionBuilder();
        public Identity? Identity => conn.Identity;
        public ConnectionId ConnectionId => conn.ConnectionId;
        public event Action<ReducerEventContext, Exception>? OnUnhandledReducerError
        {
            add => Reducers.InternalOnUnhandledReducerError += value;
            remove => Reducers.InternalOnUnhandledReducerError -= value;
        }

        internal SubscriptionEventContext(DbConnection conn)
        {
            this.conn = conn;
        }
    }

    public sealed class ProcedureEventContext : IProcedureEventContext, IRemoteDbContext
    {
        private readonly DbConnection conn;
        public readonly ProcedureEvent Event;

        public RemoteTables Db => conn.Db;
        public RemoteReducers Reducers => conn.Reducers;
        public RemoteProcedures Procedures => conn.Procedures;
        public bool IsActive => conn.IsActive;
        public void Disconnect()
        {
            conn.Disconnect();
        }
        public SubscriptionBuilder SubscriptionBuilder() => conn.SubscriptionBuilder();
        public Identity? Identity => conn.Identity;
        public ConnectionId ConnectionId => conn.ConnectionId;
        public event Action<ReducerEventContext, Exception>? OnUnhandledReducerError
        {
            add => Reducers.InternalOnUnhandledReducerError += value;
            remove => Reducers.InternalOnUnhandledReducerError -= value;
        }

        internal ProcedureEventContext(DbConnection conn, ProcedureEvent Event)
        {
            this.conn = conn;
            this.Event = Event;
        }
    }

    public sealed class SubscriptionBuilder
    {
        private readonly IDbConnection conn;

        private event Action<SubscriptionEventContext>? Applied;
        private event Action<ErrorContext, Exception>? Error;

        public SubscriptionBuilder(IDbConnection conn)
        {
            this.conn = conn;
        }

        public SubscriptionBuilder OnApplied(
            Action<SubscriptionEventContext> callback
        )
        {
            Applied += callback;
            return this;
        }

        public SubscriptionBuilder OnError(
            Action<ErrorContext, Exception> callback
        )
        {
            Error += callback;
            return this;
        }

        public TypedSubscriptionBuilder AddQuery<TRow>(
            Func<QueryBuilder, global::SpacetimeDB.IQuery<TRow>> build
        )
        {
            var typed = new TypedSubscriptionBuilder(conn, Applied, Error);
            return typed.AddQuery(build);
        }

        public SubscriptionHandle Subscribe(
            string[] querySqls
        ) => new(conn, Applied, Error, querySqls);

        public SubscriptionHandle SubscribeToAllTables() =>
            new(conn, Applied, Error, QueryBuilder.AllTablesSqlQueries());
    }

    public sealed class SubscriptionHandle : SubscriptionHandleBase<SubscriptionEventContext, ErrorContext>
    {
        public SubscriptionHandle(
            IDbConnection conn,
            Action<SubscriptionEventContext>? onApplied,
            Action<ErrorContext, Exception>? onError,
            string[] querySqls
        ) : base(conn, onApplied, onError, querySqls)
        { }
    }

    public sealed class QueryBuilder
    {
        public From From { get; } = new();

        internal static string[] AllTablesSqlQueries() => new string[]
        {
            new QueryBuilder().From.MatchResult().ToSql(),
            new QueryBuilder().From.Pairing().ToSql(),
            new QueryBuilder().From.Player().ToSql(),
            new QueryBuilder().From.QueueEntry().ToSql(),
        }
        ;
    }

    public sealed class From
    {
        public global::SpacetimeDB.Table<MatchResult, MatchResultCols, MatchResultIxCols> MatchResult() => new("match_result", new MatchResultCols("match_result"), new MatchResultIxCols("match_result"));
        public global::SpacetimeDB.Table<Pairing, PairingCols, PairingIxCols> Pairing() => new("pairing", new PairingCols("pairing"), new PairingIxCols("pairing"));
        public global::SpacetimeDB.Table<Player, PlayerCols, PlayerIxCols> Player() => new("player", new PlayerCols("player"), new PlayerIxCols("player"));
        public global::SpacetimeDB.Table<QueueEntry, QueueEntryCols, QueueEntryIxCols> QueueEntry() => new("queue_entry", new QueueEntryCols("queue_entry"), new QueueEntryIxCols("queue_entry"));
    }

    public sealed class TypedSubscriptionBuilder
    {
        private readonly IDbConnection conn;
        private Action<SubscriptionEventContext>? Applied;
        private Action<ErrorContext, Exception>? Error;
        private readonly List<string> querySqls = new();

        internal TypedSubscriptionBuilder(IDbConnection conn, Action<SubscriptionEventContext>? applied, Action<ErrorContext, Exception>? error)
        {
            this.conn = conn;
            Applied = applied;
            Error = error;
        }

        public TypedSubscriptionBuilder OnApplied(Action<SubscriptionEventContext> callback)
        {
            Applied += callback;
            return this;
        }

        public TypedSubscriptionBuilder OnError(Action<ErrorContext, Exception> callback)
        {
            Error += callback;
            return this;
        }

        public TypedSubscriptionBuilder AddQuery<TRow>(Func<QueryBuilder, global::SpacetimeDB.IQuery<TRow>> build)
        {
            var qb = new QueryBuilder();
            querySqls.Add(build(qb).ToSql());
            return this;
        }

        public SubscriptionHandle Subscribe() => new(conn, Applied, Error, querySqls.ToArray());
    }

    public abstract partial class Reducer
    {
        private Reducer() { }
    }

    public abstract partial class Procedure
    {
        private Procedure() { }
    }

    public sealed class DbConnection : DbConnectionBase<DbConnection, RemoteTables, Reducer>
    {
        public override RemoteTables Db { get; }
        public readonly RemoteReducers Reducers;
        public readonly RemoteProcedures Procedures;

        public DbConnection()
        {
            Db = new(this);
            Reducers = new(this);
            Procedures = new(this);
        }

        protected override IEventContext ToEventContext(Event<Reducer> Event) =>
        new EventContext(this, Event);

        protected override IReducerEventContext ToReducerEventContext(ReducerEvent<Reducer> reducerEvent) =>
        new ReducerEventContext(this, reducerEvent);

        protected override ISubscriptionEventContext MakeSubscriptionEventContext() =>
        new SubscriptionEventContext(this);

        protected override IErrorContext ToErrorContext(Exception exception) =>
        new ErrorContext(this, exception);

        protected override IProcedureEventContext ToProcedureEventContext(ProcedureEvent procedureEvent) =>
        new ProcedureEventContext(this, procedureEvent);

        protected override bool Dispatch(IReducerEventContext context, Reducer reducer)
        {
            var eventContext = (ReducerEventContext)context;
            return reducer switch
            {
                Reducer.JoinQueue args => Reducers.InvokeJoinQueue(eventContext, args),
                Reducer.LeaveQueue args => Reducers.InvokeLeaveQueue(eventContext, args),
                Reducer.PublishHostAddress args => Reducers.InvokePublishHostAddress(eventContext, args),
                Reducer.ReportMatch args => Reducers.InvokeReportMatch(eventContext, args),
                Reducer.SetName args => Reducers.InvokeSetName(eventContext, args),
                _ => throw new ArgumentOutOfRangeException("Reducer", $"Unknown reducer {reducer}")
            };
        }

        public SubscriptionBuilder SubscriptionBuilder() => new(this);
        public event Action<ReducerEventContext, Exception> OnUnhandledReducerError
        {
            add => Reducers.InternalOnUnhandledReducerError += value;
            remove => Reducers.InternalOnUnhandledReducerError -= value;
        }
    }
}
