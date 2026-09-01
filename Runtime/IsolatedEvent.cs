using System;
using UnityEngine;

namespace LiminalLabs.Interaction
{
    /// <summary>
    /// A C# event whose listeners are called one at a time, each inside its own try/catch,
    /// without allocating to do it.
    ///
    /// A multicast delegate invoked directly stops at the first listener that throws, and
    /// the usual cure, <c>GetInvocationList()</c>, allocates an array on every call - which
    /// on focus changes and interactions is an allocation on the hot path, several times a
    /// second per interactor. This keeps the listeners in an array that is replaced when one
    /// subscribes or unsubscribes (rare) and only read when the event fires (often). A
    /// listener that adds or removes listeners while being invoked never disturbs the loop,
    /// because the loop holds the array as it was when the invoke began.
    ///
    /// Semantics are those of a C# event: order of subscription, duplicates invoked as many
    /// times as they were added, unsubscribing a delegate that was never added is a no-op.
    /// A struct, so it lives inline in its owner and costs nothing until the first listener.
    /// </summary>
    internal struct IsolatedEvent<T>
    {
        private Action<T>[] listeners;

        public int Count => listeners?.Length ?? 0;

        public void Add(Action<T> listener)
        {
            if (listener == null) return;

            if (listeners == null)
            {
                listeners = new[] { listener };
                return;
            }

            var grown = new Action<T>[listeners.Length + 1];
            Array.Copy(listeners, grown, listeners.Length);
            grown[listeners.Length] = listener;
            listeners = grown;
        }

        public void Remove(Action<T> listener)
        {
            if (listener == null || listeners == null) return;

            int index = Array.IndexOf(listeners, listener);
            if (index < 0) return;

            if (listeners.Length == 1)
            {
                listeners = null;
                return;
            }

            var shrunk = new Action<T>[listeners.Length - 1];
            Array.Copy(listeners, 0, shrunk, 0, index);
            Array.Copy(listeners, index + 1, shrunk, index, listeners.Length - index - 1);
            listeners = shrunk;
        }

        /// <summary>Calls every listener. One that throws is logged, naming
        /// <paramref name="what"/> and <paramref name="owner"/>, and the rest still run.</summary>
        public void Invoke(T argument, string what, UnityEngine.Object owner)
        {
            Action<T>[] snapshot = listeners;
            if (snapshot == null) return;

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i](argument);
                }
                catch (Exception exception)
                {
                    Report(what, owner, exception);
                }
            }
        }

        internal static void Report(string what, UnityEngine.Object owner, Exception exception)
        {
            string name = owner != null ? owner.name : "?";
            Debug.LogError($"[Interaction] '{name}': {what} listener threw — remaining listeners still run.\n{exception}", owner);
        }
    }

    /// <summary>Two-argument twin of <see cref="IsolatedEvent{T}"/>, for (previous, next)
    /// style notifications.</summary>
    internal struct IsolatedEvent<T1, T2>
    {
        private Action<T1, T2>[] listeners;

        public int Count => listeners?.Length ?? 0;

        public void Add(Action<T1, T2> listener)
        {
            if (listener == null) return;

            if (listeners == null)
            {
                listeners = new[] { listener };
                return;
            }

            var grown = new Action<T1, T2>[listeners.Length + 1];
            Array.Copy(listeners, grown, listeners.Length);
            grown[listeners.Length] = listener;
            listeners = grown;
        }

        public void Remove(Action<T1, T2> listener)
        {
            if (listener == null || listeners == null) return;

            int index = Array.IndexOf(listeners, listener);
            if (index < 0) return;

            if (listeners.Length == 1)
            {
                listeners = null;
                return;
            }

            var shrunk = new Action<T1, T2>[listeners.Length - 1];
            Array.Copy(listeners, 0, shrunk, 0, index);
            Array.Copy(listeners, index + 1, shrunk, index, listeners.Length - index - 1);
            listeners = shrunk;
        }

        public void Invoke(T1 first, T2 second, string what, UnityEngine.Object owner)
        {
            Action<T1, T2>[] snapshot = listeners;
            if (snapshot == null) return;

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i](first, second);
                }
                catch (Exception exception)
                {
                    IsolatedEvent<T1>.Report(what, owner, exception);
                }
            }
        }
    }
}
