using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectR
{
    // Developer tool: F1 opens a list of the route's spaces, a number key jumps there.
    // Works only in the Editor and in Development builds; a release build ignores it.
    public sealed class DevSpaceJump : MonoBehaviour
    {
        [SerializeField] private ApartmentRoute route;
        [SerializeField, Tooltip("Font with Korean glyphs for the panel. Empty: the default font.")]
        private Font font;
        [SerializeField] private Key toggleKey = Key.F1;

        private static readonly Key[] DigitKeys =
            { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        private static readonly Key[] NumpadKeys =
            { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };

        private bool open;
        private GUIStyle style;

        private void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
                enabled = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || route == null)
                return;

            if (keyboard[toggleKey].wasPressedThisFrame)
                open = !open;
            // Only while playing: not under the title screen or a menu.
            if (!open || Time.timeScale == 0f)
                return;

            int count = Mathf.Min(route.SpaceCount, DigitKeys.Length);
            for (int i = 0; i < count; i++)
            {
                if (keyboard[DigitKeys[i]].wasPressedThisFrame || keyboard[NumpadKeys[i]].wasPressedThisFrame)
                {
                    route.JumpTo(i);
                    open = false;
                    return;
                }
            }
        }

        private void OnGUI()
        {
            if (!open || route == null)
                return;

            style ??= new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, richText = true };
            var text = new System.Text.StringBuilder("<b>개발자 이동</b>  (F1 닫기)\n");
            if (Time.timeScale == 0f)
                text.Append("<color=#ff8080>게임 중일 때만 이동할 수 있어요</color>\n");
            for (int i = 0; i < route.SpaceCount; i++)
            {
                string name = i == 0 ? "시작 침실" : $"{i:00}번 집";
                string line = $"{i}   {name}";
                text.Append(i == route.CurrentSpace ? $"<color=#ffd060>{line}  ← 현재</color>\n" : line + "\n");
            }

            GUI.Box(new Rect(16, 16, 260, 40 + route.SpaceCount * 24), GUIContent.none);
            GUI.Label(new Rect(28, 22, 240, 40 + route.SpaceCount * 24), text.ToString(), style);
        }
    }
}
