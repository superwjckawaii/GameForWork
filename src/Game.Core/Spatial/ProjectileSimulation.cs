using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Simulation;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private sealed class ProjectileAction(NodeCombatRequest request, ResolvedSkill skill,
        SkillConfiguration configuration, Point origin, int multiplier, EquipmentActionContext context,
        string primaryTarget, bool cohunt)
    {
        public NodeCombatRequest Request { get; } = request;
        public ResolvedSkill Skill { get; } = skill;
        public SkillConfiguration Configuration { get; } = configuration;
        public Point Origin { get; } = origin;
        public int Multiplier { get; } = multiplier;
        public EquipmentActionContext Context { get; } = context;
        public string PrimaryTarget { get; } = primaryTarget;
        public bool Cohunt { get; } = cohunt;
        public int PrimaryHits { get; set; }
        public HashSet<string> SuccessfulHits { get; } = [];
        public ResolvedHeroHit? ReferenceHit { get; set; }
        public bool Star { get; init; }
        public HashSet<string> OutboundHits { get; } = [];
        public HashSet<string> ReturnHits { get; } = [];
    }

    private sealed class PendingProjectile(ProjectileAction action, string targetId, Point position, int tick)
    {
        public ProjectileAction Action { get; } = action;
        public string TargetId { get; set; } = targetId;
        public Point Position { get; set; } = position;
        public Point Destination { get; set; } = position;
        public int Chains { get; set; }
        public int Pierces { get; set; }
        public bool Forked { get; set; }
        public bool Returning { get; set; }
        public HashSet<string> OutboundHits { get; } = [];
        public HashSet<string> ReturnHits { get; } = [];
        public int PrimaryMultiplier { get; set; } = 10_000;
        public int StartedAt { get; } = tick;
    }

    private static void LaunchProjectiles(NodeCombatRequest request, ResolvedSkill skill, SkillConfiguration configuration,
        EnemyUnit target, IEnumerable<EnemyUnit> enemies, Point origin, int multiplier, int tick,
        IList<PendingProjectile> projectiles)
    {
        bool cohunt = SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Attack) &&
            request.EquipmentRuntime!.BeginProjectileAction(target.EntityId);
        EquipmentActionContext context = request.EquipmentRuntime!.CaptureAction();
        request.Actions?.Begin(context.Id, skill, request.Build, tick, context.Triggered);
        var action = new ProjectileAction(request, skill, configuration, origin, multiplier, context, target.EntityId, cohunt);
        if (cohunt)
        {
            for (int index = 0; index < Math.Max(1, skill.ProjectileCount); index++)
                projectiles.Add(new(action, target.EntityId, origin, tick) { Destination = ExtendRay(origin, target.Position, skill.RangeRaw) });
            return;
        }
        EnemyUnit[] targets = enemies.Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, skill.RangeRaw))
            .OrderBy(enemy => enemy != target).ThenBy(enemy => Point.DistanceSquared(origin, enemy.Position))
            .Take(Math.Max(1, skill.ProjectileCount)).ToArray();
        foreach (EnemyUnit enemy in targets) projectiles.Add(new(action, enemy.EntityId, origin, tick)
            { Destination = ExtendRay(origin, enemy.Position, skill.RangeRaw) });
    }

    private static void ResolveProjectiles(IList<PendingProjectile> projectiles,
        IList<EnemyUnit> enemies, ResourceState hero, Point heroPosition, Pcg32 random, int tick, ICollection<SpatialEvent> events)
    {
        ProjectileAction[] actions = projectiles.Select(projectile => projectile.Action).Distinct().ToArray();
        foreach (PendingProjectile projectile in projectiles.ToArray())
        {
            ProjectileAction action = projectile.Action;
            ResolvedSkill skill = action.Skill;
            if (tick - projectile.StartedAt > 400) { projectiles.Remove(projectile); continue; }
            int step = Math.Max(1, skill.ProjectileSpeedRawPerSecond / 20);
            if (projectile.Returning)
            {
                Point before = projectile.Position;
                projectile.Position = Point.MoveToward(before, heroPosition, step);
                foreach (EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 &&
                             CanHit(enemy, returning: true) && OnSegment(enemy.Position, before, projectile.Position, 450)).ToArray())
                {
                    action.ReturnHits.Add(enemy.EntityId);
                    projectile.ReturnHits.Add(enemy.EntityId);
                    Hit(enemy, action.Request.EquipmentRuntime!.Has("鸦群答卷") ? 13_000 : 10_000);
                }
                if (projectile.Position == heroPosition) projectiles.Remove(projectile);
                continue;
            }
            if (action.Star)
            {
                var tracked = enemies.FirstOrDefault(enemy => enemy.EntityId == projectile.TargetId && enemy.Life > 0);
                if (tracked is null) { Finish(); continue; }
                projectile.Destination = tracked.Position;
            }
            Point previous = projectile.Position;
            projectile.Position = Point.MoveToward(previous, projectile.Destination, step);
            bool redirected = false;
            foreach (var target in enemies.Where(enemy => enemy.Life > 0 && CanHit(enemy, false) &&
                         (!action.Star || enemy.EntityId == action.PrimaryTarget) && OnSegment(enemy.Position, previous, projectile.Position, 450))
                         .OrderBy(enemy => Point.DistanceSquared(previous, enemy.Position)).ToArray())
            {
                action.OutboundHits.Add(target.EntityId);
                projectile.OutboundHits.Add(target.EntityId);
                if (action.Cohunt && target.EntityId == action.PrimaryTarget)
                    projectile.PrimaryMultiplier = action.PrimaryHits++ == 0 ? 10_000 : 4_000;
                Hit(target, 10_000);
                EnemyUnit[] candidates = enemies.Where(enemy => enemy.Life > 0 && !action.OutboundHits.Contains(enemy.EntityId))
                    .OrderBy(enemy => Point.DistanceSquared(target.Position, enemy.Position)).ToArray();
                if (!projectile.Forked && skill.ForkCount > 0)
                {
                    foreach (EnemyUnit forkTarget in candidates.Take(skill.ForkCount))
                        projectiles.Add(new PendingProjectile(action, forkTarget.EntityId, target.Position, tick)
                            { Forked = true, Destination = ExtendRay(target.Position, forkTarget.Position, skill.RangeRaw) });
                    projectiles.Remove(projectile);
                    redirected = true; break;
                }
                if (projectile.Pierces < skill.PierceCount) { projectile.Pierces++; continue; }
                EnemyUnit? next = projectile.Chains < skill.MaximumChains + (action.Context.FallingStar ? 3 : 0)
                    ? candidates.FirstOrDefault(enemy => InRange(target.Position, enemy.Position, ChainRange)) : null;
                if (next is not null)
                {
                    projectile.Chains++;
                    projectile.TargetId = next.EntityId;
                    projectile.Position = target.Position;
                    projectile.Destination = ExtendRay(target.Position, next.Position, ChainRange);
                }
                else Finish();
                redirected = true; break;
            }
            if (!redirected && projectile.Position == projectile.Destination) Finish();

            void Finish()
            {
                if (skill.Returns) projectile.Returning = true;
                else projectiles.Remove(projectile);
            }
            bool CanHit(EnemyUnit enemy, bool returning)
            {
                if ((action.Cohunt || action.Star) && enemy.EntityId == action.PrimaryTarget)
                    return !(returning ? projectile.ReturnHits : projectile.OutboundHits).Contains(enemy.EntityId);
                return !(returning ? action.ReturnHits : action.OutboundHits).Contains(enemy.EntityId);
            }
            void Hit(EnemyUnit enemy, int returnMultiplier)
            {
                var kind = skill.SkillId == SkillIds.SpiritBlade
                    ? projectile.Chains > 0 ? SpatialEventKind.ChainHit : SpatialEventKind.SpiritBladeHit
                    : SpatialEventKind.SkillEffect;
                action.Request.EquipmentRuntime!.InAction(action.Context, () =>
                {
                    if (action.Star && action.ReferenceHit is { } original)
                    {
                        ApplyHeroDamage(action.Request, skill, action.Configuration, enemy, hero, random, tick,
                            action.Origin, original.Damage.Scale(3_500), original.Critical, events);
                    }
                    else
                    {
                        ResolvedHeroHit? hit = ResolveHeroHit(action.Request, skill, action.Configuration, enemy, hero, random, tick,
                            action.Origin, ScaleCombatValue(ScaleCombatValue(action.Multiplier, returnMultiplier),
                                enemy.EntityId == action.PrimaryTarget ? projectile.PrimaryMultiplier : 10_000), events,
                            eventKind: kind, chainIndex: projectile.Chains);
                        if (hit is not null)
                        {
                            action.SuccessfulHits.Add(enemy.EntityId);
                            action.ReferenceHit ??= hit;
                        }
                    }
                });
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, 0,
                    projectile.Position, enemy.Position, $"action:{action.Context.Id}|projectile:{(action.Star ? "star" : projectile.Returning ? "return" : "outbound")}|chain:{projectile.Chains}|cohunt:{action.Cohunt}|scale:{projectile.PrimaryMultiplier}"));
            }
        }
        foreach (ProjectileAction action in actions.Where(action => !projectiles.Any(projectile => projectile.Action == action)))
        {
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", action.PrimaryTarget, 0,
                action.Origin, action.Origin, $"action:{action.Context.Id}|projectile:completed|unique-hits:{action.SuccessfulHits.Count}"));
            if (action.Star || action.Context.Triggered || action.ReferenceHit is null ||
                !SkillDefinitions.Get(action.Skill.SkillId).Tags.HasFlag(SkillTag.Attack) ||
                !action.Request.EquipmentRuntime!.Has("逐星者余响") ||
                !enemies.Any(enemy => enemy.EntityId == action.PrimaryTarget && enemy.Life > 0)) continue;
            var star = new ProjectileAction(action.Request,
                action.Skill with { Returns = false, PierceCount = 0, ForkCount = 0, MaximumChains = 0 },
                action.Configuration, action.Origin, action.Multiplier,
                action.Request.EquipmentRuntime.CreateTriggeredAction(action.PrimaryTarget, copy: true), action.PrimaryTarget, false)
            { Star = true, ReferenceHit = action.ReferenceHit };
            for (int index = 0; index < Math.Min(5, action.SuccessfulHits.Count); index++)
                projectiles.Add(new(star, action.PrimaryTarget, action.Origin, tick));
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", action.PrimaryTarget,
                Math.Min(5, action.SuccessfulHits.Count), action.Origin, action.Origin,
                $"action:{star.Context.Id}|projectile:star-launched|source-action:{action.Context.Id}"));
        }
    }

    private static Point ExtendRay(Point origin, Point aim, int range)
    {
        double dx = aim.XRaw - origin.XRaw, dy = aim.YRaw - origin.YRaw;
        double length = Math.Sqrt(dx * dx + dy * dy);
        return length == 0 ? new(origin.XRaw + range, origin.YRaw) :
            new(origin.XRaw + (int)Math.Round(dx * range / length), origin.YRaw + (int)Math.Round(dy * range / length));
    }

    private static bool OnSegment(Point point, Point start, Point end, int radius)
    {
        double dx = end.XRaw - start.XRaw, dy = end.YRaw - start.YRaw;
        double length = dx * dx + dy * dy;
        double t = length == 0 ? 0 : Math.Clamp(((point.XRaw - start.XRaw) * dx + (point.YRaw - start.YRaw) * dy) / length, 0, 1);
        double x = point.XRaw - (start.XRaw + t * dx), y = point.YRaw - (start.YRaw + t * dy);
        return x * x + y * y <= (double)radius * radius;
    }
}
