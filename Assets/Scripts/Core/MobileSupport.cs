using UnityEngine;

namespace SeasonRush
{
    /// <summary>
    /// Phone / tablet support. Does nothing on a PC.
    ///
    /// * On Android / iOS the game is locked to landscape, runs at 60 fps and keeps the screen awake while playing.
    /// * <see cref="SafeInsets"/> tells the UI how far to stay away from notches, camera cut-outs, rounded corners and
    ///   the gesture bar, so the score panels, the joystick and the ATTACK button are never hidden.
    /// </summary>
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
            Screen.orientation = ScreenOrientation.AutoRotation;       // flips between the two landscape sides

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;             // the screen must not dim in the middle of a match
        }

        /// <summary>
        /// Distance from each screen edge to the safe area, in the UI's virtual units (a screen 1080 units high):
        /// x = left, y = top, z = right, w = bottom. All zeros on a normal PC screen.
        /// </summary>
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
