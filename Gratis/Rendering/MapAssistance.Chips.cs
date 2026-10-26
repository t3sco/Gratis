using System;
using System.Numerics;
#if GRATIS_PREVIEW
using ImGuiNET;
#else
using Dalamud.Bindings.ImGui;
#endif

namespace Gratis.Rendering;

public sealed partial class MapAssistance {
    public AetheryteChoice? Chip { get; private set; }
    public Vector2 ChipButtonCenter { get; private set; }
    public string ChipMessage { get; private set; } = "";
    public Vector2 ChipDismissButtonCenter { get; private set; }
    public Vector4 ChipBounds { get; private set; }
    private AetheryteChoice? retainedChoice;
    private Vector4 retainedChipBounds;
    private SpotlightRequest? dismissedTarget;
    private string? chipId;
    private bool chipConfirm, chipRequiresTravelActions;
    private SpotlightRequest? chipTarget;
    private double chipCreated, chipUntil, chipDrawn, chipArmedAt;
    public float ChipTimeRemaining { get; private set; }

    private void DrawAetheryteChip(IMapWindowHost host) {
        var previousBounds = ChipBounds;
        Chip = null;
        ChipBounds = Vector4.Zero;
        var request = ContextTarget(host) ?? StoryTarget(host)
            ?? (host.Renderer.Spotlight.Active ? host.Renderer.Spotlight.Request : null);
        if (!TravelReady || hovered != null || !host.Config.AetheryteChips || request is not { } target
            || target == dismissedTarget
            || !host.Config.TravelActions && (IsContextTarget(target) || target.Source == SpotlightSource.Quest)
            || (!AetheryteSelection.Useful(target.MapId, target.Target, places.Context.MapId, places.Context.Player)
                && !IsContextTarget(target))) {
            DrawChipExit();
            return;
        }
        if (chipTarget != target) {
            chipTarget = target;
            chipCreated = host.Now;
            chipUntil = host.Now + 8;
            chipDrawn = host.Now;
        }
        var over = previousBounds != Vector4.Zero
            && ImGui.IsMouseHoveringRect(new(previousBounds.X, previousBounds.Y),
                new(previousBounds.Z, previousBounds.W), false);
        if (over || chipConfirm) {
            chipUntil += Math.Max(0, host.Now - chipDrawn);
            if (IsContextTarget(target))
                contextTargetUntil = Math.Max(contextTargetUntil, chipUntil);
            if (storyTarget == target)
                storyUntil = Math.Max(storyUntil, chipUntil);
        }
        chipDrawn = host.Now;
        ChipTimeRemaining = (float)Math.Clamp(chipUntil - host.Now, 0, 8);
        if (ChipTimeRemaining <= 0) {
            dismissedTarget = target;
            DrawChipExit();
            return;
        }
        chipRequiresTravelActions = IsContextTarget(target) || target.Source == SpotlightSource.Quest;
        Chip = AetheryteSelection.Nearest(places.Entries, target.MapId, target.Target, places.Access,
            provider.ActiveAetheryte, available && host.Config.TravelActions);
        if (Chip is not { } choice) {
            DrawChipExit();
            return;
        }
        if (chipId != choice.Destination.Id) {
            chipId = choice.Destination.Id;
            ChipMessage = "";
            chipConfirm = false;
        }
        var s = GratisTheme.ChromeScale;
        var (min, max) = ChromeLayout.SafeBounds();
        var size = TeleportCardSize(choice.Destination.Name, max.X - min.X);
        if (size.X < 230 * s) {
            DrawChipExit();
            return;
        }
        ChipBounds = ChromeLayout.PlacePopover("nearest-travel", size,
            host.Renderer.WorldToScreen(target.Target, ImGui.GetWindowPos()));
        if (ChipBounds == Vector4.Zero) {
            DrawChipExit();
            return;
        }
        if (DismissPopover(ChipBounds, chipCreated)) {
            dismissedTarget = target;
            Chip = null;
            panelBounds.Clear();
            InputActive = false;
            ChipBounds = Vector4.Zero;
            DrawChipExit();
            return;
        }
        retainedChoice = choice;
        retainedChipBounds = ChipBounds;
        var position = ChromeLayout.Position(ChipBounds);
        DrawPointer(ChipBounds, host.Renderer.WorldToScreen(target.Target, ImGui.GetWindowPos()));
        Panel("##nearest-aetheryte", position, size, () => {
            if (!chipConfirm)
                chipArmedAt = 0;
            else if (chipArmedAt == 0)
                chipArmedAt = host.Now;
            var cost = places.Access(choice.Destination).Cost;
            var card = new TeleportCardModel(choice.Destination.Name,
                TravelMeta(choice.Destination.Subtitle, choice.Destination.Name,
                    choice.Aethernet ? "Aethernet" : "Nearest aetheryte"),
                chipConfirm ? "" : ChipMessage, cost, choice.Aethernet, chipConfirm,
                host.Now > chipCreated && (!chipConfirm || host.Now - chipArmedAt >= ConfirmDelay));
            if (DrawTeleportCard(card, out var close))
                RequestChip(choice, chipConfirm);
            ChipButtonCenter = TravelButtonCenter;
            ChipDismissButtonCenter = TravelCloseCenter;
            if (close) {
                dismissedTarget = target;
                Chip = null;
                panelBounds.Clear();
                InputActive = false;
            }
        }, pivot: host.Renderer.WorldToScreen(target.Target, ImGui.GetWindowPos()));
    }

    private void DrawChipExit() {
        var alpha = UiMotion.Visibility(ImGui.GetID("##nearest-aetheryte"), false);
        if (retainedChoice is not { } choice || alpha <= .001f || retainedChipBounds == Vector4.Zero)
            return;
        var b = retainedChipBounds;
        var size = new Vector2(b.Z - b.X, b.W - b.Y);
        Panel("##nearest-aetheryte", new(b.X, b.Y), size, () => {
            DrawTeleportCard(new(choice.Destination.Name,
                TravelMeta(choice.Destination.Subtitle, choice.Destination.Name,
                    choice.Aethernet ? "Aethernet" : "Nearest aetheryte"),
                "", places.Access(choice.Destination).Cost, choice.Aethernet, false, false), out _);
        }, interactive: false);
    }

    public string RequestChip(AetheryteChoice choice, bool confirmed = false) {
        places.Refresh();
        if (chipRequiresTravelActions && !MapDrawContext.Current.Config.TravelActions || !TravelReady
            || !places.Access(choice.Destination).Attuned || provider.IsBusy)
            return ChipMessage = "Unable to teleport now.";
        ChipMessage = choice.Aethernet ? Request(choice.Destination, confirmed)
            : places.Teleport(choice.Destination, confirmed);
        chipConfirm = ChipMessage.StartsWith("Confirm ", StringComparison.Ordinal);
        return ChipMessage;
    }
}