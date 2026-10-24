using System.Collections.Generic;
using World.Devices;

namespace OWT.DamageMeter.Tracking
{
    /// <summary>
    /// Ambient "who caused this" context. Game code often deals proc damage with a null
    /// harm_source (lightning, explosions, darts...). We push the triggering device around
    /// the game call that spawns the proc, and the damage hook reads it back.
    /// </summary>
    internal static class DamageOrigin
    {
        private struct Scope
        {
            public Device Device;
            public bool Derived;
        }

        private static readonly List<Scope> Stack = new List<Scope>(16);

        public static bool HasScope
        {
            get { return Stack.Count > 0; }
        }

        public static Device Current
        {
            get { return Stack.Count > 0 ? Stack[Stack.Count - 1].Device : null; }
        }

        public static bool CurrentDerived
        {
            get { return Stack.Count > 0 && Stack[Stack.Count - 1].Derived; }
        }

        public static void Push(Device device, bool derived)
        {
            // Harmony postfixes always run in practice, but a skipped/aborted patch chain
            // should not be able to grow the stack without bound.
            if (Stack.Count > 64)
            {
                Stack.Clear();
            }
            Stack.Add(new Scope { Device = device, Derived = derived });
        }

        public static void Pop()
        {
            if (Stack.Count > 0)
            {
                Stack.RemoveAt(Stack.Count - 1);
            }
        }

        public static void Reset()
        {
            Stack.Clear();
        }
    }
}
