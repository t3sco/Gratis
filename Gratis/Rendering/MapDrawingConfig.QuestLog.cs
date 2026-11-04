using System;

namespace Gratis.Rendering;

public partial class MapDrawingConfig {
    public bool QuestLogOpenWithMap = false;
    public bool QuestLogPinned = false;
    public bool QuestLogKeepOpenOnShow = false;
    public bool QuestLogRememberSearch = false;
    public float QuestLogWidth = 320;
    public bool QuestLogReplaceJournal = false;
    public bool QuestLogShowLocked = false;
    public float EffectiveQuestLogWidth => Math.Clamp(float.IsFinite(QuestLogWidth) ? QuestLogWidth : 320, 240, 440);
    public void ResetQuestLog() {
        QuestLogOpenWithMap = false;
        QuestLogPinned = false;
        QuestLogKeepOpenOnShow = false;
        QuestLogRememberSearch = false;
        QuestLogWidth = 320;
        QuestLogReplaceJournal = false;
        QuestLogShowLocked = false;
    }
}
