namespace OpenGarrison.PluginHost;

public sealed record OpenGarrisonPluginManifestPlanResult<TPlugin>(
    IReadOnlyList<TPlugin> Plugins,
    IReadOnlyList<string> Warnings);

public static class OpenGarrisonPluginManifestPlanner
{
    public static OpenGarrisonPluginManifestPlanResult<TPlugin> PlanLoadOrder<TPlugin>(
        IEnumerable<TPlugin> plugins,
        Func<TPlugin, OpenGarrisonPluginManifest> manifestSelector)
    {
        var candidates = plugins
            .Select((plugin, index) => new Candidate<TPlugin>(plugin, manifestSelector(plugin), index))
            .ToList();
        var warnings = new List<string>();

        var activeCandidates = RemoveUnavailableCandidates(candidates, warnings);
        var orderedCandidates = SortCandidates(activeCandidates, warnings);
        return new OpenGarrisonPluginManifestPlanResult<TPlugin>(
            orderedCandidates.Select(static candidate => candidate.Plugin).ToArray(),
            warnings);
    }

    private static List<Candidate<TPlugin>> RemoveUnavailableCandidates<TPlugin>(
        List<Candidate<TPlugin>> candidates,
        List<string> warnings)
    {
        var activeCandidates = candidates.ToList();
        var changed = true;
        while (changed)
        {
            changed = false;
            var activeIds = activeCandidates
                .Select(static candidate => candidate.Manifest.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var activeById = activeCandidates.ToDictionary(
                static candidate => candidate.Manifest.Id,
                static candidate => candidate,
                StringComparer.OrdinalIgnoreCase);

            for (var index = activeCandidates.Count - 1; index >= 0; index -= 1)
            {
                var candidate = activeCandidates[index];
                OpenGarrisonPluginManifestDependency? missingDependency = candidate.Manifest.Dependencies.FirstOrDefault(dependency =>
                    !activeIds.Contains(dependency.Id)
                    || !DependencyVersionMatches(activeById[dependency.Id].Manifest.Version, dependency.Version));
                if (missingDependency is not null)
                {
                    warnings.Add(string.IsNullOrWhiteSpace(missingDependency.Version)
                        ? $"[plugin] skipped \"{candidate.Manifest.Id}\" because required dependency \"{missingDependency.Id}\" is not loaded."
                        : $"[plugin] skipped \"{candidate.Manifest.Id}\" because required dependency \"{missingDependency.Id}\" version \"{missingDependency.Version}\" is not loaded.");
                    activeCandidates.RemoveAt(index);
                    changed = true;
                    continue;
                }

                var conflictId = candidate.Manifest.Conflicts.FirstOrDefault(activeIds.Contains);
                if (!string.IsNullOrWhiteSpace(conflictId))
                {
                    warnings.Add($"[plugin] skipped \"{candidate.Manifest.Id}\" because it conflicts with loaded plugin \"{conflictId}\".");
                    activeCandidates.RemoveAt(index);
                    changed = true;
                }
            }
        }

        return activeCandidates;
    }

    private static List<Candidate<TPlugin>> SortCandidates<TPlugin>(
        List<Candidate<TPlugin>> candidates,
        List<string> warnings)
    {
        var candidatesById = candidates.ToDictionary(
            static candidate => candidate.Manifest.Id,
            static candidate => candidate,
            StringComparer.OrdinalIgnoreCase);
        var incomingCounts = candidates.ToDictionary(
            static candidate => candidate.Manifest.Id,
            static _ => 0,
            StringComparer.OrdinalIgnoreCase);
        var outgoingEdges = candidates.ToDictionary(
            static candidate => candidate.Manifest.Id,
            static _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            foreach (var dependency in candidate.Manifest.Dependencies.Concat(candidate.Manifest.OptionalDependencies))
            {
                if (candidatesById.ContainsKey(dependency.Id))
                {
                    AddEdge(outgoingEdges, incomingCounts, dependency.Id, candidate.Manifest.Id);
                }
            }

            foreach (var afterId in candidate.Manifest.LoadOrder.After)
            {
                if (candidatesById.ContainsKey(afterId))
                {
                    AddEdge(outgoingEdges, incomingCounts, afterId, candidate.Manifest.Id);
                }
            }

            foreach (var beforeId in candidate.Manifest.LoadOrder.Before)
            {
                if (candidatesById.ContainsKey(beforeId))
                {
                    AddEdge(outgoingEdges, incomingCounts, candidate.Manifest.Id, beforeId);
                }
            }
        }

        var ready = candidates
            .Where(candidate => incomingCounts[candidate.Manifest.Id] == 0)
            .OrderBy(static candidate => candidate.OriginalIndex)
            .ToList();
        var ordered = new List<Candidate<TPlugin>>(candidates.Count);

        while (ready.Count > 0)
        {
            var candidate = ready[0];
            ready.RemoveAt(0);
            ordered.Add(candidate);

            foreach (var targetId in outgoingEdges[candidate.Manifest.Id].OrderBy(id => candidatesById[id].OriginalIndex))
            {
                incomingCounts[targetId] -= 1;
                if (incomingCounts[targetId] != 0)
                {
                    continue;
                }

                ready.Add(candidatesById[targetId]);
                ready.Sort(static (left, right) => left.OriginalIndex.CompareTo(right.OriginalIndex));
            }
        }

        if (ordered.Count == candidates.Count)
        {
            return ordered;
        }

        var orderedIds = ordered
            .Select(static candidate => candidate.Manifest.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cyclicCandidates = candidates
            .Where(candidate => !orderedIds.Contains(candidate.Manifest.Id))
            .OrderBy(static candidate => candidate.OriginalIndex)
            .ToList();
        warnings.Add("[plugin] plugin load-order cycle detected; preserving discovery order for unresolved plugins: "
            + string.Join(", ", cyclicCandidates.Select(static candidate => candidate.Manifest.Id)));
        ordered.AddRange(cyclicCandidates);
        return ordered;
    }

    private static bool DependencyVersionMatches(string loadedVersion, string? requestedVersion)
    {
        return string.IsNullOrWhiteSpace(requestedVersion)
            || string.Equals(loadedVersion, requestedVersion.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void AddEdge(
        Dictionary<string, HashSet<string>> outgoingEdges,
        Dictionary<string, int> incomingCounts,
        string fromId,
        string toId)
    {
        if (string.Equals(fromId, toId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (outgoingEdges[fromId].Add(toId))
        {
            incomingCounts[toId] += 1;
        }
    }

    private sealed record Candidate<TPlugin>(
        TPlugin Plugin,
        OpenGarrisonPluginManifest Manifest,
        int OriginalIndex);
}
