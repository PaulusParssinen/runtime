// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// CycleTimer has methods related to getting cycle timer values.
// It uses an all-statics class as a namespace mechanism.
//

#ifndef _CYCLETIMER_H_
#define _CYCLETIMER_H_

#include "windef.h"

class CycleTimer
{
   // This returns the value of the *non-thread-virtualized* cycle counter.
    static uint64_t GetCycleCount64();


    // This wraps GetCycleCount64 in the signature of QueryThreadCycleTime -- but note
    // that it ignores the "thrd" argument.
    static BOOL WINAPI DefaultQueryThreadCycleTime(_In_ HANDLE thrd, _Out_ PULONG64 cyclesPtr);

    // The function pointer type for QueryThreadCycleTime.
    typedef BOOL (WINAPI *QueryThreadCycleTimeSig)(_In_ HANDLE, _Out_ PULONG64);

    // Returns a function pointer for QueryThreadCycleTime, or else BadFPtr.
    static QueryThreadCycleTimeSig GetQueryThreadCycleTime();

    // Initialized once from NULL to either BadFPtr or QueryThreadCycleTime.
    static QueryThreadCycleTimeSig s_QueryThreadCycleTimeFPtr;

  public:

    // This method computes the number of cycles/sec for the current machine.  The cycles are those counted
    // by GetThreadCycleTime; we assume that these are of equal duration, though that is not necessarily true.
    // If any OS interaction fails, returns 0.0.
    static double CyclesPerSecond();

    // Does a large number of queries, and returns the average of their overhead, so other measurements
    // can adjust for this.
    static uint64_t QueryOverhead();

    // There's no "native" atomic add for 64 bit, so we have this convenience function.
    static void InterlockedAddU64(uint64_t* loc, uint64_t amount);

    // Attempts to query the cycle counter of the current thread.  If successful, returns "true" and sets
    // *cycles to the cycle counter value.  Otherwise, returns false.  Note that the value returned is (currently)
    // virtualized to the current thread only on Windows; on non-windows x86/x64 platforms, directly reads
    // the cycle counter and returns that value.
    static bool GetThreadCyclesS(uint64_t* cycles);
};

#endif // _CYCLETIMER_H_

