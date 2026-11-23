
#nullable enable

using System;
using SpacetimeDB.ClientApi;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SpacetimeDB.Types
{
    public sealed partial class RemoteReducers : RemoteBase
    {
        public delegate void JoinQueueHandler(ReducerEventContext ctx);
        public event JoinQueueHandler? OnJoinQueue;

        public void JoinQueue()
        {
            conn.InternalCallReducer(new Reducer.JoinQueue());
        }

        public bool InvokeJoinQueue(ReducerEventContext ctx, Reducer.JoinQueue args)
        {
            if (OnJoinQueue == null)
            {
                if (InternalOnUnhandledReducerError != null)
                {
                    switch (ctx.Event.Status)
                    {
                        case Status.Failed(var reason): InternalOnUnhandledReducerError(ctx, new Exception(reason)); break;
                        case Status.OutOfEnergy(var _): InternalOnUnhandledReducerError(ctx, new Exception("out of energy")); break;
                        case Status.UnknownResult(var _): InternalOnUnhandledReducerError(ctx, new UnknownResultException()); break;
                    }
                }
                return false;
            }
            OnJoinQueue(
                ctx
            );
            return true;
        }
    }

    public abstract partial class Reducer
    {
        [SpacetimeDB.Type]
        [DataContract]
        public sealed partial class JoinQueue : Reducer, IReducerArgs
        {
            string IReducerArgs.ReducerName => "join_queue";
        }
    }
}
