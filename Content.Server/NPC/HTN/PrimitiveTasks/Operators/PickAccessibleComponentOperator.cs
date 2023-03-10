using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC.Pathfinding;
using Robust.Shared.Map;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Picks a nearby component that is accessible.
/// </summary>
public sealed class PickAccessibleComponentOperator : HTNOperator
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    private PathfindingSystem _pathfinding = default!;
    private EntityLookupSystem _lookup = default!;

    [DataField("rangeKey", required: true)]
    public string RangeKey = string.Empty;

    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    [DataField("targetMoveKey", required: true)]
    public string TargetMoveKey = string.Empty;

    [DataField("component", required: true)]
    public string Component = string.Empty;

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
    }

    /// <inheritdoc/>
    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        // Check if the component exists
        if (!_factory.TryGetRegistration(Component, out var registration))
        {
            return (false, null);
        }

        var range = blackboard.GetValueOrDefault<float>(RangeKey, _entManager);
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coordinates, _entManager))
        {
            return (false, null);
        }

        var compType = registration.Type;
        var query = _entManager.GetEntityQuery(compType);
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var targets = new List<EntityUid>();

        // TODO: Need to get ones that are accessible.
        // TODO: Look at unreal HTN to see repeatable ones maybe?
        // TODO: Need type
        foreach (var entity in _lookup.GetEntitiesInRange(coordinates, range))
        {
            if (entity == owner || !query.TryGetComponent(entity, out var comp))
                continue;

            targets.Add(entity);
        }

        if (targets.Count == 0)
        {
            return (false, null);
        }

        blackboard.TryGetValue<float>(RangeKey, out var maxRange, _entManager);

        if (maxRange == 0f)
            maxRange = 7f;

        foreach (var target in targets)
        {
            if (!xformQuery.TryGetComponent(target, out var xform))
                continue;

            var targetCoords = xform.Coordinates;
            var path = await _pathfinding.GetPath(owner, target, range, cancelToken);
            if (path.Result != PathResult.Path)
            {
                continue;
            }

            return (true, new Dictionary<string, object>()
            {
                { TargetKey, target },
                { TargetMoveKey, targetCoords },
                { PathfindKey, path}
            });
        }

        return (false, null);
    }
}
