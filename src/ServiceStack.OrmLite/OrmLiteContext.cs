﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Runtime.Remoting.Messaging;
using System.Threading;

namespace ServiceStack.OrmLite
{
    public class OrmLiteContext
    {
        public static readonly OrmLiteContext Instance = new OrmLiteContext();

        /// <summary>
        /// Tell ServiceStack to use ThreadStatic Items Collection for Context Scoped items.
        /// Warning: ThreadStatic Items aren't pinned to the same request in async services which callback on different threads.
        /// </summary>
        public static bool UseThreadStatic = false;

        [ThreadStatic]
        public static IDictionary ContextItems;

        /// <summary>
        /// Gets a list of items for this context. 
        /// </summary>
        public virtual IDictionary Items
        {
            get
            {
                return GetItems() ?? (CreateItems());
            }
            set
            {
                CreateItems(value);
            }
        }

        private const string _key = "__OrmLite.Items";

        private IDictionary GetItems()
        {
            try
            {
                if (UseThreadStatic)
                    return ContextItems;

                return CallContext.LogicalGetData(_key) as IDictionary;
            }
            catch (NotImplementedException)
            {
                //Fixed in Mono master: https://github.com/mono/mono/pull/817
                return CallContext.GetData(_key) as IDictionary;
            }
        }

        private IDictionary CreateItems(IDictionary items = null)
        {
            try
            {
                if (UseThreadStatic)
                {
                    ContextItems = items ?? (items = new Dictionary<object, object>());
                }
                else
                {
                    CallContext.LogicalSetData(_key, items ?? (items = new ConcurrentDictionary<object, object>()));
                }
            }
            catch (NotImplementedException)
            {
                //Fixed in Mono master: https://github.com/mono/mono/pull/817
                CallContext.SetData(_key, items ?? (items = new ConcurrentDictionary<object, object>()));
            }
            return items;
        }

        public T GetOrCreate<T>(Func<T> createFn)
        {
            if (Items.Contains(typeof(T).Name))
                return (T)Items[typeof(T).Name];

            return (T)(Items[typeof(T).Name] = createFn());
        }

        internal static void SetItem<T>(string key, T value)
        {
            if (Equals(value, default(T)))
            {
                Instance.Items.Remove(key);
            }
            else
            {
                Instance.Items[key] = value;
            }
        }

        public static OrmLiteState CreateNewState()
        {
            var state = new OrmLiteState();
            Instance.Items["OrmLiteState"] = state;
            return state;
        }

        public static OrmLiteState GetOrCreateState()
        {
            return (Instance.Items["OrmLiteState"] as OrmLiteState)
                ?? CreateNewState();
        }

        public static OrmLiteState OrmLiteState
        {
            get
            {
                return Instance.Items["OrmLiteState"] as OrmLiteState;
            }
            set
            {
                SetItem("OrmLiteState", value);
            }
        }

        //Only used when using OrmLite API's against a native IDbConnection (i.e. not from DbFactory) 
        internal static IDbTransaction TSTransaction
        {
            get
            {
                var state = OrmLiteState;
                return state != null
                    ? state.TSTransaction
                    : null;
            }
            set { GetOrCreateState().TSTransaction = value; }
        }
    }

    public class OrmLiteState
    {
        private static long Counter;
        public long Id;

        public OrmLiteState()
        {
            Id = Interlocked.Increment(ref Counter);
        }

        public IDbTransaction TSTransaction;
        public IOrmLiteResultsFilter ResultsFilter;

        public override string ToString()
        {
            return "State Id: {0}".Fmt(Id);
        }
    }
}