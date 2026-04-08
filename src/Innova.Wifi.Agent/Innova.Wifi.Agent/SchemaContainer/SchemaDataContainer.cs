/*
 * INTEL CONFIDENTIAL
 *
 * Copyright (C) 2023 Intel Corporation
 *
 * This software and the related documents are Intel copyrighted materials, and
 * your use of them is governed by the express license under which they were
 * provided to you (License). Unless the License provides otherwise, you may not
 * use, modify, copy, publish, distribute, disclose or transmit this software or
 * the related documents without Intel's prior written permission.
 *
 * This software and the related documents are provided as is, with no express or
 * implied warranties, other than those that are expressly stated in the License.
 *
 */

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Intel.Telemetry.Api.Context.Schema;
using Intel.Telemetry.Api.Message.Version;

namespace Intel.Telemetry.Test.App.SchemaContainer;

internal class SchemaDataContainer
{
    private readonly ConcurrentDictionary<(string, MessageVersion), ISchemaDataEntry> _schemaDataEntriesMap;

    /// <summary>
    ///
    /// </summary>
    public SchemaDataContainer()
    {
        _schemaDataEntriesMap = new ConcurrentDictionary<(string name, MessageVersion version), ISchemaDataEntry>();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="dataEntries"></param>
    internal void AddOrUpdateSchemaEntries(IEnumerable<ISchemaDataEntry> dataEntries)
    {
        // Copy dataEntries
        var dataEntriesInternal = dataEntries.ToImmutableList();

        foreach (var schemaDataEntry in dataEntriesInternal)
            _schemaDataEntriesMap.AddOrUpdate((schemaDataEntry.Name, schemaDataEntry.Version), s => schemaDataEntry,
                (_, _) => schemaDataEntry);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="tuple"></param>
    /// <returns></returns>
    internal bool IsEntryAvailable((string entryName, MessageVersion version) tuple)
    {
        return _schemaDataEntriesMap.TryGetValue(tuple, out var schemaDataEntry) &&
               schemaDataEntry.Version == tuple.version && schemaDataEntry.Available;
    }
}