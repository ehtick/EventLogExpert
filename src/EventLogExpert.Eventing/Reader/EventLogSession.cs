﻿// // Copyright (c) Microsoft Corporation.
// // Licensed under the MIT License.

using EventLogExpert.Eventing.Helpers;
using EventLogExpert.Eventing.Models;
using System.Runtime.InteropServices;

namespace EventLogExpert.Eventing.Reader;

public sealed partial class EventLogSession : IDisposable
{
    private EventLogSession() { }

    public static EventLogSession GlobalSession { get; } = new();

    internal EventLogHandle Handle { get; } = EventLogHandle.Zero;

    internal EventLogHandle SystemRenderContext { get; } = CreateRenderContext(EvtRenderContextFlags.System);

    internal EventLogHandle UserRenderContext { get; } = CreateRenderContext(EvtRenderContextFlags.User);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public EventLogInformation GetLogInformation(string logName, PathType pathType) => new(this, logName, pathType);

    /// <summary>Gets an ordered list of all the log names on the system.</summary>
    public IEnumerable<string> GetLogNames()
    {
        List<string> paths = [];

        using EventLogHandle channelHandle = EventMethods.EvtOpenChannelEnum(Handle, 0);
        int error = Marshal.GetLastWin32Error();

        if (channelHandle.IsInvalid)
        {
            EventMethods.ThrowEventLogException(error);
        }

        bool doneReading = false;

        do
        {
            string path = NextChannelPath(channelHandle, ref doneReading);

            if (!doneReading)
            {
                paths.Add(path);
            }
        }
        while (!doneReading);

        return paths.Order();
    }

    public HashSet<string> GetProviderNames()
    {
        HashSet<string> providers = [];

        using EventLogHandle providerHandle = EventMethods.EvtOpenPublisherEnum(Handle, 0);
        int error = Marshal.GetLastWin32Error();

        if (providerHandle.IsInvalid)
        {
            EventMethods.ThrowEventLogException(error);
        }

        bool doneReading = false;

        do
        {
            string path = NextPublisherId(providerHandle, ref doneReading);

            if (!doneReading)
            {
                providers.Add(path);
            }
        }
        while (!doneReading);

        return providers;
    }

    private static EventLogHandle CreateRenderContext(EvtRenderContextFlags renderContextFlags)
    {
        EventLogHandle renderContextHandle = EventMethods.EvtCreateRenderContext(0, null, renderContextFlags);
        int error = Marshal.GetLastWin32Error();

        if (renderContextHandle.IsInvalid)
        {
            renderContextHandle.Dispose();

            EventMethods.ThrowEventLogException(error);
        }

        return renderContextHandle;
    }

    private static string NextChannelPath(EventLogHandle channelHandle, ref bool doneReading)
    {
        bool success = EventMethods.EvtNextChannelPath(channelHandle, 0, null, out int bufferSize);
        int error = Marshal.GetLastWin32Error();

        if (!success)
        {
            if (error == Interop.ERROR_NO_MORE_ITEMS)
            {
                doneReading = true;

                return string.Empty;
            }

            if (error != Interop.ERROR_INSUFFICIENT_BUFFER)
            {
                EventMethods.ThrowEventLogException(error);
            }
        }

        Span<char> buffer = stackalloc char[bufferSize];

        success = EventMethods.EvtNextChannelPath(channelHandle, bufferSize, buffer, out bufferSize);
        error = Marshal.GetLastWin32Error();

        if (!success)
        {
            EventMethods.ThrowEventLogException(error);
        }

        return bufferSize - 1 <= 0 ? string.Empty : new string(buffer[..(bufferSize - 1)]);
    }

    private static string NextPublisherId(EventLogHandle publisherHandle, ref bool doneReading)
    {
        bool success = EventMethods.EvtNextPublisherId(publisherHandle, 0, null, out int bufferSize);
        int error = Marshal.GetLastWin32Error();

        if (!success)
        {
            if (error == Interop.ERROR_NO_MORE_ITEMS)
            {
                doneReading = true;

                return string.Empty;
            }

            if (error != Interop.ERROR_INSUFFICIENT_BUFFER)
            {
                EventMethods.ThrowEventLogException(error);
            }
        }

        Span<char> buffer = stackalloc char[bufferSize];

        success = EventMethods.EvtNextPublisherId(publisherHandle, bufferSize, buffer, out bufferSize);
        error = Marshal.GetLastWin32Error();

        if (!success)
        {
            EventMethods.ThrowEventLogException(error);
        }

        return bufferSize - 1 <= 0 ? string.Empty : new string(buffer[..(bufferSize - 1)]);
    }

    private void Dispose(bool disposing)
    {
        if (Handle is { IsInvalid: false })
        {
            Handle.Dispose();
        }

        if (SystemRenderContext is { IsInvalid: false })
        {
            SystemRenderContext.Dispose();
        }

        if (UserRenderContext is { IsInvalid: false })
        {
            UserRenderContext.Dispose();
        }
    }
}