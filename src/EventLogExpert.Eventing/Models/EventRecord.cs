﻿// // Copyright (c) Microsoft Corporation.
// // Licensed under the MIT License.

using EventLogExpert.Eventing.Helpers;
using System.Security.Principal;

namespace EventLogExpert.Eventing.Models;

public sealed record EventRecord
{
    public string PathName { get; set; } = string.Empty;

    public PathType PathType { get; set; }

    public Guid? ActivityId { get; set; }

    public string ComputerName { get; set; } = string.Empty;

    public ushort Id { get; set; }

    public long? Keywords { get; set; }

    public byte? Level { get; set; }

    public string LogName { get; set; } = string.Empty;

    public int? ProcessId { get; set; }

    public long? RecordId { get; set; }

    public IEnumerable<object> Properties { get; set; } = [];

    public string ProviderName { get; set; } = string.Empty;

    public ushort? Task { get; set; }

    public int? ThreadId { get; set; }

    public DateTime TimeCreated { get; set; }

    public SecurityIdentifier? UserId { get; set; }

    public byte? Version { get; set; }

    public string? Xml { get; set; }

    public string? Error { get; init; }

    public bool IsSuccess => string.IsNullOrEmpty(Error);
}
