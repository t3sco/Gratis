using System;
using System.Numerics;
using Gratis.Classes;

namespace Gratis.Rendering;

public sealed partial class Spotlight {
    public bool Matches(MarkerInfo marker) => Request is { } request && marker.Scale > 0
        && (request.MarkerId is null || marker.ObjectiveId == request.MarkerId || marker.DataId == request.MarkerId)
        && (request.Icon is null || marker.IconId == request.Icon || marker.IconId == DrawHelpers.QuestionMarkIcon)
        && Vector2.DistanceSquared(marker.Position / marker.Scale, TargetTexture) < 4;

    private const float HideSeconds = 2, HideEase = .6f;

    public float MarkerAlpha(MarkerInfo marker) => !Visible || Matches(marker)
        || MarkerRelevance.IsProtected(marker) || PlayerDrawing.IsPlayerMarker(marker)
        ? 1 : 1 - (HideOthers ? float.Lerp(.6f, .94f, HideAmount) : .6f) * Opacity;

    // Full for the first couple of seconds, then eases back to the ordinary dimmed level.
    private float HideAmount => Math.Clamp(1 - (Age - HideSeconds) / HideEase, 0, 1);

    public float MarkerBreath(MarkerInfo marker) => Matches(marker) && Pulse
        && !MapDrawContext.Current.Config.ReduceMotion
        ? 1 + .055f * Opacity * MathF.Sin(Age * MathF.Tau / 1.6f) : 1;
}