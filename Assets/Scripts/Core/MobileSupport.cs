using UnityEngine;

namespace SeasonRush
{
    public static class MobileSupport
    {
        public static bool IsMobile => Application.isMobilePlatform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (!Application.isMobilePlatform) return;

            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
        public static Vector4 SafeInsets()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f) return Vector4.zero;
            float scale = Screen.height / 1080f;
            return new Vector4(
                safe.xMin / scale,
                (Screen.height - safe.yMax) / scale,
                (Screen.width - safe.xMax) / scale,
                safe.yMin / scale);
        }
    }
}
