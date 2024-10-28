﻿// // Copyright (c) Microsoft Corporation.
// // Licensed under the MIT License.

using EventLogExpert.Eventing.Helpers;
using EventLogExpert.Eventing.Models;
using System.Runtime.InteropServices;

namespace EventLogExpert.Eventing.Reader;

public sealed partial class EventLogReader(string path, PathType pathType) : IDisposable
{
    private readonly EventLogHandle _handle =
        EventMethods.EvtQuery(EventLogSession.GlobalSession.Handle, path, null, (int)pathType);
    private readonly SemaphoreSlim _semaphore = new(1);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public bool TryGetEvents(out EventProperties[] events, int batchSize = 100)
    {
        var buffer = new IntPtr[batchSize];
        int count = 0;

        _semaphore.Wait();

        try
        {
            bool success = EventMethods.EvtNext(_handle, batchSize, buffer, 0, 0, ref count);

            if (!success)
            {
                events = [];
                return false;
            }
        }
        finally
        {
            _semaphore.Release();
        }

        events = new EventProperties[count];

        for (int i = 0; i < count; i++)
        {
            using var eventHandle = new EventLogHandle(buffer[i]);

            events[i] = RenderEvent(eventHandle, EvtRenderFlags.EventValues);
        }

        return true;
    }

    private static EventProperties RenderEvent(EventLogHandle eventHandle, EvtRenderFlags flag)
    {
        IntPtr buffer = IntPtr.Zero;

        try
        {
            bool success = EventMethods.EvtRender(
                EventLogSession.GlobalSession.RenderContext,
                eventHandle,
                flag,
                0,
                IntPtr.Zero,
                out int bufferUsed,
                out int propertyCount);

            int error = Marshal.GetLastWin32Error();

            if (!success && error != 122 /* ERROR_INSUFFICIENT_BUFFER */)
            {
                EventMethods.ThrowEventLogException(error);
            }

            buffer = Marshal.AllocHGlobal(bufferUsed);

            success = EventMethods.EvtRender(
                EventLogSession.GlobalSession.RenderContext,
                eventHandle,
                flag,
                bufferUsed,
                buffer,
                out bufferUsed,
                out propertyCount);

            error = Marshal.GetLastWin32Error();

            if (!success)
            {
                EventMethods.ThrowEventLogException(error);
            }

            return EventMethods.GetEventProperties(buffer, propertyCount);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void Dispose(bool disposing)
    {
        _handle.Dispose();
    }
}
