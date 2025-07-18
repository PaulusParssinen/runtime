// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using TestLibrary;

namespace TestUnhandledException
{
    public delegate void MyCallback();

    unsafe class Program
    {
        [DllImport("unhandlednative")]
        public static extern void InvokeCallbackOnNewThread(delegate*unmanaged<void> callBack);

        private const string INTERNAL_CALL = "__internal";

        [SuppressGCTransition]
        [DllImport(INTERNAL_CALL, EntryPoint = "HelloCpp")]
        private static extern void Test();

        [UnmanagedCallersOnly]
        static void ThrowException()
        {
            SetDllResolver();
            Test();
        }

        private static void SetDllResolver()
        {
            NativeLibrary.SetDllImportResolver(
                Assembly.GetExecutingAssembly(),
                static (library, _, _) =>
                    library == INTERNAL_CALL ? NativeLibrary.GetMainProgramHandle() : IntPtr.Zero
            );
        }

        static void Main(string[] args)
        {
            // Ensure that the OS doesn't generate core dump for this intentionally crashing process
            Utilities.DisableOSCoreDump();

            if (args[0] == "main")
            {
                throw new Exception("Test");
            }
            if (args[0] == "mainhardware")
            {
                string s = null;
                Console.WriteLine(s.Length); // This will cause a NullReferenceException
            }
            else if (args[0] == "foreign")
            {
                InvokeCallbackOnNewThread(&ThrowException);
            }
            else if (args[0] == "secondary")
            {
                Thread t = new Thread(() => throw new Exception("Test"));
                t.Start();
                t.Join();
            }
            else if (args[0] == "secondaryhardware")
            {
                Thread t = new Thread(() =>
                {
                    string s = null;
                    Console.WriteLine(s.Length); // This will cause a NullReferenceException
                });
                t.Start();
                t.Join();
            }
            else if (args[0] == "secondaryunhandled")
            {
                Thread t = new Thread(() => throw new Exception("Test"));
                t.Start();
                t.Join();
            }
        }
    }
}
