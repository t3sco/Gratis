using System;
using System.Numerics;
using Gratis.Controllers;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance
{
    public AetheryteChoice? Chip { get; private set; }
    public Vector2 ChipButtonCenter { get; private set; }
    public string ChipMessage { get; private set; } = "";
    public Vector2 ChipDismissButtonCenter { get; private set; }
    public Vector4 ChipBounds { get; private set; }
    private SpotlightRequest? dismissedTarget;
    private string? chipId;
    private bool chipConfirm, chipRequiresTravelActions;
    private SpotlightRequest? chipTarget;
    private double chipCreated, chipUntil, chipDrawn;
    public float ChipTimeRemaining { get; private set; }

    private void DrawAetheryteChip(IMapWindowHost host)
    {
        var previousBounds = ChipBounds;
        Chip = null; ChipBounds = Vector4.Zero;
        var request = ContextTarget(host) ?? StoryTarget(host) ?? (host.Renderer.Spotlight.Active ? host.Renderer.Spotlight.Request : null);
        if (!host.Config.AetheryteChips || request is not { } target || target == dismissedTarget ||
            !host.Config.TravelActions && (IsContextTarget(target) || target.Source == SpotlightSource.Quest) ||
            (!AetheryteSelection.Useful(target.MapId, target.Target, places.Context.MapId, places.Context.Player) && !IsContextTarget(target))) return;
        if (chipTarget != target) { chipTarget = target; chipCreated = host.Now; chipUntil = host.Now + 8; chipDrawn = host.Now; }
        var over = previousBounds != Vector4.Zero && ImGui.IsMouseHoveringRect(new(previousBounds.X, previousBounds.Y), new(previousBounds.Z, previousBounds.W), false);
        if (over || chipConfirm) {
            chipUntil += Math.Max(0, host.Now - chipDrawn);
            if (IsContextTarget(target)) contextTargetUntil = Math.Max(contextTargetUntil, chipUntil);
            if (storyTarget == target) storyUntil = Math.Max(storyUntil, chipUntil);
        }
        chipDrawn = host.Now;
        ChipTimeRemaining = (float)Math.Clamp(chipUntil - host.Now, 0, 8);
        if (ChipTimeRemaining <= 0) { dismissedTarget = target; return; }
        chipRequiresTravelActions = IsContextTarget(target) || target.Source == SpotlightSource.Quest;
        Chip = AetheryteSelection.Nearest(places.Entries, target.MapId, target.Target, places.Access,
            provider.ActiveAetheryte, available && host.Config.TravelActions);
        if (Chip is not { } choice) return;
        if (chipId != choice.Destination.Id) { chipId = choice.Destination.Id; ChipMessage = ""; chipConfirm = false; }
        var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.SafeBounds();
        var size = new Vector2(MathF.Min(310 * s, max.X - min.X),
            (ChipMessage.Length > 0 && !chipConfirm ? 148 : 104) * s);
        if (size.X < 230 * s) return;
        ChipBounds = ChromeLayout.Place("nearest-travel", size, host.Config.TravelCardAnchor);
        if (ChipBounds == Vector4.Zero) return;
        var position = ChromeLayout.Position(ChipBounds);
        Panel("##nearest-aetheryte", position, size, () => {
            CardHeading("Nearest aetheryte", choice.Destination.Name,
                choice.Aethernet ? "Aethernet" : places.Access(choice.Destination).Cost is { } gil ? $"{gil:N0} gil" : "Cost unavailable");
            ImGui.BeginDisabled(host.Now <= chipCreated);
            if (PrimaryTravelButton(chipConfirm ? "Confirm teleport" : choice.Aethernet ? "Travel here" : "Teleport", GratisIcons.Compass))
                RequestChip(choice, chipConfirm);
            ImGui.EndDisabled();
            ChipButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            ImGui.SameLine(0, GratisTheme.Gap * s);
            if (QuietButton("Dismiss")) { dismissedTarget = target; Chip = null; }
            ChipDismissButtonCenter = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
            if (ChipMessage.Length > 0 && !chipConfirm) ImGui.TextWrapped(ChipMessage);
            if (!host.Config.ReduceMotion) {
                var width = (size.X - 24 * s) * ChipTimeRemaining / 8;
                var start = position + new Vector2(12 * s, size.Y - 4 * s);
                ImGui.GetWindowDrawList().AddLine(start, start + new Vector2(width, 0), ImGui.GetColorU32(GratisTheme.WarmAccent with { W = .5f }), s);
            }
        });
    }

    public string RequestChip(AetheryteChoice choice, bool confirmed = false)
    {
        places.Refresh();
        if (chipRequiresTravelActions && !MapDrawContext.Current.Config.TravelActions || !canTravel() || !places.Access(choice.Destination).Attuned || provider.IsBusy)
            return ChipMessage = "Unable to teleport now.";
        ChipMessage = choice.Aethernet ? Request(choice.Destination, confirmed) : places.Teleport(choice.Destination, confirmed);
        chipConfirm = ChipMessage.StartsWith("Confirm ", StringComparison.Ordinal);
        return ChipMessage;
    }
}
