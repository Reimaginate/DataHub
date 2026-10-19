using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reimaginate.DataHub.DataAccess.Commands.CommitRebaseCheckpoint;
using Reimaginate.DataHub.Requests.Internal.InitTrackedEntity;
using Reimaginate.DataHub.Services.RebaseTracking;
using Reimaginate.DataHub.SharedModels.Constants;
using Reimaginate.DataHub.SharedModels.Core;
using Reimaginate.Mediator;

namespace Reimaginate.DataHub.Helpers;

internal sealed record RebaseTrackingPlan(
    InitTrackedEntityRequest ReplacementBaseline,
    List<ChangeTrackingEntry> EntriesToDelete,
    RebaseEntityTrackingResult Result,
    string IncompleteReason = null,
    ChangeTrackingEntry RetainedBaseline = null,
    bool UseStoredEntityRecovery = false,
    bool IncludeArchivedTrackingEntries = true);

internal static class RebaseTrackingPersistence
{
    public static JObject CreateBaselineData(JObject entityData, ChangeTrackingEntry originalBaseline = null)
    {
        ArgumentNullException.ThrowIfNull(entityData);
        var data = ChangeTrackingHelper.StripBaseProperties(entityData);
        var createdOn = entityData[nameof(DataHubEntity.createdOn)];
        if (createdOn == null || createdOn.Type == JTokenType.Null)
        {
            createdOn = originalBaseline?.Data?[nameof(DataHubEntity.createdOn)];
            if ((createdOn == null || createdOn.Type == JTokenType.Null) && originalBaseline != null)
                createdOn = new JValue(originalBaseline.Timestamp);
        }

        // The checkpoint timestamp advances; the entity's creation date must not.
        if (createdOn != null && createdOn.Type != JTokenType.Null)
            data[nameof(DataHubEntity.createdOn)] = createdOn.DeepClone();
        return data;
    }

    public static async Task PersistAsync(IMediator mediator, List<RebaseTrackingPlan> plans, CancellationToken cancellationToken, Action ensureLease = null)
    {
        ensureLease?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        await Parallel.ForEachAsync(plans, new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8)
        }, async (plan, ct) => await PersistEntityAsync(mediator, plan, ct, ensureLease));
    }

    private static async Task PersistEntityAsync(IMediator mediator, RebaseTrackingPlan plan, CancellationToken ct, Action ensureLease)
    {
        var remaining = plan.EntriesToDelete.OrderBy(entry => entry.Timestamp).ToList();
        var originalInit = plan.RetainedBaseline ?? remaining.LastOrDefault(entry => entry.EntryType == ChangeTrackingEntryTypes.Init);
        ChangeTrackingEntry checkpointBaseline = null;
        var finalTimestamp = plan.ReplacementBaseline?.Timestamp ?? plan.RetainedBaseline?.Timestamp;
        plan.Result.RebasedTo = null;
        plan.Result.ArchivedTrackingEntries = [];
        plan.Result.ArchivedTrackingEntryCount = 0;
        var identity = plan.ReplacementBaseline;
        plan.Result.DataSource ??= identity?.DataSource ?? originalInit?.DataSource;
        plan.Result.EntityType ??= identity?.EntityType ?? originalInit?.EntityType;
        try
        {
            if (plan.UseStoredEntityRecovery)
            {
                await PersistStoredEntityRecoveryAsync(mediator, plan, remaining, originalInit, ct, ensureLease);
                return;
            }

            while (remaining.Count != 0)
            {
                ensureLease?.Invoke();
                ct.ThrowIfCancellationRequested();
                var replacing = plan.ReplacementBaseline != null;
                var deleteBudget = CosmosRebaseCheckpointStore.MaxOperations - (replacing ? 1 : 0) - (checkpointBaseline != null ? 1 : 0);
                var candidates = new List<List<ChangeTrackingEntry>>();
                if (!replacing)
                {
                    // Older Init entries must disappear together. Identical Init entries
                    // at the retained timestamp can be removed in chunks: any copies left
                    // behind still reconstruct exactly the retained state.
                    if (plan.RetainedBaseline == null || remaining.Any(entry =>
                        entry.Timestamp >= plan.RetainedBaseline.Timestamp && !IsIdenticalBaseline(entry, plan.RetainedBaseline)))
                    {
                        Fail(plan, "Cannot safely remove history relative to the retained baseline. Only older entries or identical Init entries at the same timestamp can be cleaned up; other history was preserved for separate recovery.");
                        return;
                    }
                    var oldInits = remaining.Where(entry => entry.EntryType == ChangeTrackingEntryTypes.Init &&
                        entry.Timestamp < plan.RetainedBaseline.Timestamp).ToList();
                    if (oldInits.Count <= deleteBudget)
                        candidates.Add(oldInits.Concat(remaining.Except(oldInits).Take(deleteBudget - oldInits.Count)).ToList());
                }
                else
                {
                    var prefix = new List<ChangeTrackingEntry>();
                    foreach (var group in remaining.GroupBy(entry => entry.Timestamp).OrderBy(group => group.Key))
                    {
                        if (prefix.Count + group.Count() > deleteBudget) break;
                        prefix.AddRange(group);
                        // Never leave a second, older baseline visible after committing.
                        if ((originalInit != null && group.Key >= originalInit.Timestamp &&
                             (group.Key > originalInit.Timestamp || prefix.Any(entry => entry.EntryType == ChangeTrackingEntryTypes.Update && entry.Timestamp >= originalInit.Timestamp))) ||
                            prefix.Count == remaining.Count)
                            candidates.Add(prefix.ToList());
                    }
                }

                if (candidates.Count == 0)
                {
                    Fail(plan, "No complete timestamp group fits a safe atomic checkpoint; remaining history was preserved.");
                    return;
                }

                var committed = false;
                for (var candidateIndex = candidates.Count - 1; candidateIndex >= 0; candidateIndex--)
                {
                    var prefix = candidates[candidateIndex];
                    var deleting = checkpointBaseline == null ? prefix : new[] { checkpointBaseline }.Concat(prefix).ToList();
                    ChangeTrackingEntry baseline = null;
                    if (replacing)
                    {
                        var cutoff = prefix.Last().Timestamp;
                        JObject data;
                        if (prefix.Count == remaining.Count && cutoff == finalTimestamp)
                            data = plan.ReplacementBaseline.EntityData;
                        else
                        {
                            var start = checkpointBaseline ?? originalInit;
                            if (start == null) continue;
                            var replay = new[] { start }.Concat(prefix.Where(entry => entry.EntryType != ChangeTrackingEntryTypes.Init &&
                                entry.Timestamp >= start.Timestamp)).ToList();
                            data = CreateBaselineData(ChangeTrackingHelper.ReassembleEntity(replay), start);
                        }
                        baseline = new ChangeTrackingEntry
                        {
                            DataSource = identity.DataSource, EntityType = identity.EntityType, EntityId = identity.EntityId,
                            EntryType = ChangeTrackingEntryTypes.Init, Timestamp = cutoff, Data = (JObject)data.DeepClone(),
                            pk = (checkpointBaseline ?? originalInit)?.pk
                        };
                        // Use the same known creation date even when recovery used the stored entity.
                        if (identity.EntityData[nameof(DataHubEntity.createdOn)] is { Type: not JTokenType.Null } createdOn)
                            baseline.Data[nameof(DataHubEntity.createdOn)] = createdOn.DeepClone();
                        baseline.id = CheckpointId(baseline, deleting);
                    }
                    ensureLease?.Invoke();
                    ct.ThrowIfCancellationRequested();
                    var (response, exception) = await mediator.TrySend(new CommitRebaseCheckpointCommand
                    {
                        Baseline = baseline, EntriesToDelete = deleting, EnsureLease = ensureLease
                    }, ct);
                    if (exception is OperationCanceledException) throw exception;
                    if (response?.TooLarge == true) continue; // No writes occurred; try a smaller complete prefix.
                    if (exception != null || response?.Success != true)
                    {
                        Fail(plan, exception?.Message ?? response?.FailureReason ?? "Atomic rebase checkpoint was not confirmed.");
                        return;
                    }
                    // Count only original history, never the intermediate baselines we created.
                    RecordArchives(plan, prefix);
                    plan.Result.RebasedTo = baseline?.Timestamp ?? plan.RetainedBaseline.Timestamp;
                    var removedIds = prefix.Select(entry => entry.id).ToHashSet();
                    remaining.RemoveAll(entry => removedIds.Contains(entry.id));
                    checkpointBaseline = baseline;
                    committed = true;
                    break;
                }
                if (!committed)
                {
                    Fail(plan, "A complete timestamp group or baseline exceeds the atomic transaction payload limit; remaining history was preserved.");
                    return;
                }
            }
            plan.Result.RebasedTo ??= plan.RetainedBaseline?.Timestamp;
            plan.Result.ArchivedTrackingEntries = plan.Result.ArchivedTrackingEntries.OrderBy(entry => entry.Timestamp).ToList();
            plan.Result.Success = plan.IncompleteReason == null;
            plan.Result.FailureReason = plan.IncompleteReason;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Fail(plan, $"Atomic rebase stopped: {ex.Message}"); }
    }

    private static async Task PersistStoredEntityRecoveryAsync(IMediator mediator, RebaseTrackingPlan plan,
        List<ChangeTrackingEntry> remaining, ChangeTrackingEntry originalInit, CancellationToken ct, Action ensureLease)
    {
        var snapshot = plan.ReplacementBaseline;
        if (snapshot?.Timestamp == null || remaining.Any(entry => entry.Timestamp > snapshot.Timestamp))
        {
            Fail(plan, "Stored-entity recovery requires a complete current-state cutoff; original history was preserved.");
            return;
        }

        var hadHistory = remaining.Count != 0;
        // With no history, this is a single create-only checkpoint. Otherwise,
        // installing the authoritative snapshot must remove every old Init and every
        // same-timestamp update together. All other history is older and cannot replay
        // against the new baseline, even if subsequent cleanup is interrupted.
        var required = remaining.Where(entry => entry.EntryType == ChangeTrackingEntryTypes.Init ||
            entry.Timestamp == snapshot.Timestamp).ToList();
        if (required.Count >= CosmosRebaseCheckpointStore.MaxOperations)
        {
            Fail(plan, "The recovery baseline and required history deletions exceed the atomic transaction limit; original history was preserved.");
            return;
        }

        var optional = remaining.Except(required).Take(CosmosRebaseCheckpointStore.MaxOperations - 1 - required.Count).ToList();
        while (true)
        {
            var deleting = required.Concat(optional).ToList();
            var baseline = new ChangeTrackingEntry
            {
                DataSource = snapshot.DataSource, EntityType = snapshot.EntityType, EntityId = snapshot.EntityId,
                EntryType = ChangeTrackingEntryTypes.Init, Timestamp = snapshot.Timestamp.Value,
                Data = (JObject)snapshot.EntityData.DeepClone(), pk = (originalInit ?? remaining.FirstOrDefault())?.pk
            };
            baseline.id = CheckpointId(baseline, deleting);
            var response = await CommitAsync(baseline, deleting);
            if (response?.TooLarge == true && optional.Count > 0)
            {
                optional = optional.Take(optional.Count / 2).ToList();
                continue;
            }
            if (response?.Success != true)
            {
                Fail(plan, response?.FailureReason ?? "Stored-entity recovery checkpoint was not confirmed.");
                return;
            }

            Confirm(deleting);
            plan.Result.RebasedTo = baseline.Timestamp;
            plan.Result.Warning = hadHistory
                ? "STORED_ENTITY_RECOVERY: The stored Data Hub entity was used as the authoritative baseline. Changes present only in tracking may have been discarded."
                : "STORED_ENTITY_RECOVERY: A tracking baseline was created from the stored Data Hub entity because no tracking history existed.";
            break;
        }

        while (remaining.Count > 0)
        {
            var count = Math.Min(CosmosRebaseCheckpointStore.MaxOperations, remaining.Count);
            while (true)
            {
                var deleting = remaining.Take(count).ToList();
                var response = await CommitAsync(null, deleting);
                if (response?.TooLarge == true && count > 1)
                {
                    count = Math.Max(1, count / 2);
                    continue;
                }
                if (response?.Success != true)
                {
                    Fail(plan, response?.FailureReason ?? "Stored-entity recovery cleanup was not confirmed.");
                    return;
                }
                Confirm(deleting);
                break;
            }
        }

        plan.Result.ArchivedTrackingEntries = plan.Result.ArchivedTrackingEntries.OrderBy(entry => entry.Timestamp).ToList();
        plan.Result.Success = true;
        plan.Result.FailureReason = null;

        async Task<CommitRebaseCheckpointResponse> CommitAsync(ChangeTrackingEntry baseline, List<ChangeTrackingEntry> deleting)
        {
            ensureLease?.Invoke();
            ct.ThrowIfCancellationRequested();
            var (response, exception) = await mediator.TrySend(new CommitRebaseCheckpointCommand
            {
                Baseline = baseline, EntriesToDelete = deleting, EnsureLease = ensureLease
            }, ct);
            if (exception != null) throw exception;
            return response;
        }

        void Confirm(List<ChangeTrackingEntry> deleting)
        {
            RecordArchives(plan, deleting);
            var removedIds = deleting.Select(entry => entry.id).ToHashSet();
            remaining.RemoveAll(entry => removedIds.Contains(entry.id));
        }
    }

    private static bool IsIdenticalBaseline(ChangeTrackingEntry entry, ChangeTrackingEntry retained) =>
        entry.EntryType == ChangeTrackingEntryTypes.Init && retained.EntryType == ChangeTrackingEntryTypes.Init &&
        entry.id != retained.id && entry.DataSource == retained.DataSource &&
        entry.EntityType == retained.EntityType && entry.EntityId == retained.EntityId &&
        entry.Timestamp == retained.Timestamp && entry.Data != null && retained.Data != null &&
        JToken.DeepEquals(entry.Data, retained.Data);

    private static void RecordArchives(RebaseTrackingPlan plan, List<ChangeTrackingEntry> entries)
    {
        // Count only confirmed original entries; compact responses never retain their payloads.
        plan.Result.ArchivedTrackingEntryCount += entries.Count;
        if (plan.IncludeArchivedTrackingEntries)
            plan.Result.ArchivedTrackingEntries.AddRange(entries);
    }

    private static string CheckpointId(ChangeTrackingEntry baseline, List<ChangeTrackingEntry> deleting)
    {
        var identity = new JArray(baseline.DataSource, baseline.EntityType, baseline.EntityId, baseline.Timestamp.ToUniversalTime().ToString("O"));
        identity.Add(new JArray(deleting.OrderBy(entry => entry.id, StringComparer.Ordinal).Select(entry => new JArray(entry.id, entry._etag))));
        identity.Add(Canonicalize(baseline.Data));
        return "replacement-rebase-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString(Formatting.None))));
    }

    private static JToken Canonicalize(JToken token) => token switch
    {
        JObject obj => new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => new JProperty(property.Name, Canonicalize(property.Value)))),
        JArray array => new JArray(array.Select(Canonicalize)),
        _ => token.DeepClone()
    };

    private static void Fail(RebaseTrackingPlan plan, string reason)
    {
        plan.Result.ArchivedTrackingEntries = plan.Result.ArchivedTrackingEntries.OrderBy(entry => entry.Timestamp).ToList();
        plan.Result.Success = false;
        plan.Result.FailureReason = plan.IncompleteReason == null ? reason : $"{plan.IncompleteReason} {reason}";
    }
}
