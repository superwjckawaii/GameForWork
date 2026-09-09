using Godot;
using GameForWork.GodotClient;
using GameForWork.Core.Campaign;
using GameForWork.Core.Characters;
using GameForWork.Core.Management;
using GameForWork.Core.Campaign.World;
using System.Text.Json;

// Copied as Baseline.cs by the isolated launcher; never part of shipping code.
public partial class Baseline : Node
{
    public override async void _Ready()
    {
        try
        {
            GetWindow().Size = new(960, 640);
            GetWindow().GuiDisableInput = true;
            GameSession session = GameSession.CreateNew(new("港口回放", CharacterGender.Androgynous,
                CharacterSkinTone.Fair, CharacterHairStyle.Cropped, BaseClass.Fighter), 20260908, false);
            session = GameSession.Restore(session.Capture() with { Campaign = CampaignState.CreateLegacyCompleted().Capture() });
            session.Endgame.TryCompleteFinalBreakthrough(100, true);
            session.World.Economy.AddDispositionProceeds(1000, 0);
            var build = session.World.Hero.Build;
            session.World.Hero.UpdateBuild(build with
            {
                Sheet = build.Sheet with { Level = 120, FlatMaximumLife = 100_000, FlatLifeRegeneration = 2000 },
                Weapon = build.Weapon with { MinimumPhysicalDamage = 10_000, MaximumPhysicalDamage = 15_000 },
                MovementSpeedBasisPoints = 20_000, AlwaysHit = true,
            });
            var panel = new HarborPanel { Size = new(960, 640) };
            panel.Initialize(() => session, _ => { });
            AddChild(panel);
            if (!session.StartHarbor(ExpeditionTeamKind.Hero, 1)) throw new InvalidOperationException("Replay could not start.");
            var run = session.HarborDispatchFor(ExpeditionTeamKind.Hero)!.ActiveRun!;
            foreach (var region in run.Regions)
            {
                long target = Math.Min(run.DurationMilliseconds - 1, region.StartsAtMilliseconds + 2000);
                long elapsed = session.HarborDispatchFor(ExpeditionTeamKind.Hero)!.ElapsedMilliseconds;
                session.Advance(target - elapsed);
                panel.RefreshState();
                await Capture($"harbor-region-{region.Combat.Frames[0].NodeIndex}.webp");
            }
            session.Advance(300_000);
            panel.RefreshState();
            await Capture("harbor-chest.webp");
            File.WriteAllText(ProjectSettings.GlobalizePath("res://harbor-report.json"), JsonSerializer.Serialize(new
            { run.Succeeded, run.DurationMilliseconds, Regions = run.Regions.Count, Chests = session.HarborChests.Count,
                PrototypeBuild = true, ActualRenderedFrames = Engine.GetFramesDrawn() }));
            if (!run.Succeeded || session.HarborChests.Count != 1) throw new InvalidOperationException("Replay failed.");
            GD.Print("HARBOR_REPLAY_COMPLETE"); GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
    private async Task Capture(string name)
    {
        await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        if (image.SaveWebp(ProjectSettings.GlobalizePath("res://" + name)) != Error.Ok)
            throw new IOException("Could not capture harbor replay.");
    }
}
