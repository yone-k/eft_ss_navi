using System.Collections.ObjectModel;
using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Settings;

public static class MapMarkerClusterer
{
    public const double PmcSpawnClusterRadiusMeters = 15;

    public static IReadOnlyList<MapMarker> ClusterPmcSpawns(IReadOnlyList<MapMarker> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);

        var result = markers
            .Where(marker => marker.Kind != MapMarkerKind.PmcSpawn)
            .ToList();
        var spawns = markers
            .Where(marker => marker.Kind == MapMarkerKind.PmcSpawn)
            .ToArray();
        var visited = new bool[spawns.Length];
        var radiusSquared = PmcSpawnClusterRadiusMeters * PmcSpawnClusterRadiusMeters;

        for (var index = 0; index < spawns.Length; index++)
        {
            if (visited[index])
            {
                continue;
            }

            var clusterIndices = new List<int>();
            var queue = new Queue<int>();
            visited[index] = true;
            queue.Enqueue(index);

            while (queue.Count > 0)
            {
                var currentIndex = queue.Dequeue();
                clusterIndices.Add(currentIndex);
                for (var candidateIndex = 0; candidateIndex < spawns.Length; candidateIndex++)
                {
                    if (visited[candidateIndex] ||
                        SquaredDistance(
                            spawns[currentIndex].Position,
                            spawns[candidateIndex].Position) > radiusSquared)
                    {
                        continue;
                    }

                    visited[candidateIndex] = true;
                    queue.Enqueue(candidateIndex);
                }
            }

            result.Add(new MapMarker(
                MapMarkerKind.PmcSpawn,
                null,
                new WorldPoint(
                    clusterIndices.Average(clusterIndex => spawns[clusterIndex].Position.X),
                    clusterIndices.Average(clusterIndex => spawns[clusterIndex].Position.Z))));
        }

        return new ReadOnlyCollection<MapMarker>(result);
    }

    private static double SquaredDistance(WorldPoint first, WorldPoint second)
    {
        var deltaX = first.X - second.X;
        var deltaZ = first.Z - second.Z;
        return (deltaX * deltaX) + (deltaZ * deltaZ);
    }
}
