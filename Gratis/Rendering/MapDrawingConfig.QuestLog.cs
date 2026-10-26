using System;

namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    public bool QuestLogOpenWithMap = false;
    public float QuestLogWidth = 320;
    public float EffectiveQuestLogWidth => Math.Clamp(float.IsFinite(QuestLogWidth) ? QuestLogWidth : 320, 240, 440);
    public void ResetQuestLog() {
        QuestLogOpenWithMap = false;
        QuestLogWidth = 320;
    }
}
