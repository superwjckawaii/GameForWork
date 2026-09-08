using Godot;

namespace GameForWork.GodotClient;

/// <summary>Event driven feedback; never moves controls or changes their hit rectangles.</summary>
public partial class InterfaceFeedback : Node
{
    public override void _Ready()
    {
        GetTree().NodeAdded += Register;
        Visit(GetTree().Root);
    }

    public override void _ExitTree() => GetTree().NodeAdded -= Register;
    private void Visit(Node node)
    {
        Register(node);
        foreach (Node child in node.GetChildren()) Visit(child);
    }
    private void Register(Node node)
    {
        if (node is not Button button || button is ItemCell || button.HasMeta("interface_feedback")) return;
        button.SetMeta("interface_feedback", true);
        Callable.From(() => Attach(button)).CallDeferred();
    }
    private static void Attach(Button button)
    {
        if (!IsInstanceValid(button) || button.IsQueuedForDeletion()) return;
        var highlight = new ColorRect
        {
            Name = "InteractionHighlight", MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(1f, 0.88f, 0.65f, 0f)
        };
        button.AddChild(highlight);
        highlight.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Tween? tween = null;
        void Fade(float alpha, double seconds)
        {
            tween?.Kill();
            tween = highlight.CreateTween();
            tween.TweenProperty(highlight, "color:a", button.Disabled ? 0f : alpha, seconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
        button.MouseEntered += () => Fade(0.035f, 0.12);
        button.MouseExited += () => Fade(0, 0.16);
        button.ButtonDown += () => Fade(0.12f, 0.045);
        button.ButtonUp += () => Fade(0, 0.16);
        button.VisibilityChanged += () => { tween?.Kill(); highlight.Color = new Color(1f, 0.88f, 0.65f, 0); };
    }
}
