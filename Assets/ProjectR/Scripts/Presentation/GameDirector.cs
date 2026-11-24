using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectR
{
    // Start and end of a run, plus every menu: title, settings (with the brightness check), pause and ending.
    [DefaultExecutionOrder(-10)]
    public sealed class GameDirector : MonoBehaviour
    {
        private enum Screen { Title, Playing, Paused, Settings, Brightness, Ending }

        [SerializeField] private HorrorEventManager progression;
        [SerializeField] private PlayerController player;
        [SerializeField, Tooltip("Played when the effects volume changes, so the player hears the new level.")]
        private AudioClip effectsPreview;
        [SerializeField, Tooltip("Live view shown behind the title screen.")]
        private TitleBackdrop backdrop;
        [Header("Menu look")]
        [SerializeField, Tooltip("Title and headings (Latin capitals).")] private Font titleFont;
        [SerializeField, Tooltip("Menu items and settings; must cover Korean.")] private Font uiFont;
        [SerializeField] private AudioClip uiHover;
        [SerializeField] private AudioClip uiSelect;
        [Header("Start fade")]
        [SerializeField, Min(0f), Tooltip("Title to black after START.")] private float startFadeOut = .7f;
        [SerializeField, Min(0f), Tooltip("Black held before the game appears.")] private float startHold = .25f;
        [SerializeField, Min(0f), Tooltip("Black to the game.")] private float startFadeIn = 1.1f;
        [SerializeField, Tooltip("Shown on the black screen before play. Empty: no notice.")]
        private string startNotice = "이어폰을 착용하고, 실내 조명을 끈 뒤 플레이하는 것을 권장합니다.";
        [SerializeField, Min(0f), Tooltip("How long the notice stays fully visible; any key or click skips it.")]
        private float noticeHold = 2.5f;
        [SerializeField, Min(0f)] private float noticeFade = .6f;

        private InputAction pause;
        private bool pauseRequested;
        private float fade;
        private float endingShownAt;
        private float curtain; // black over everything while the game starts
        private float noticeAlpha;
        private bool starting;
        private Screen screen = Screen.Title;
        private Screen settingsReturn = Screen.Title;
        private AudioSource previewSource;
        private float lastPreview;

        private bool korean;
        private GUIStyle titleStyle, headingStyle, itemStyle, itemSpacedStyle, labelStyle, valueStyle, hintStyle, subtitleStyle, creditStyle, noticeStyle, sliderStyle, thumbStyle;

        public bool IsEnding { get; private set; }
        public bool EndingShown { get; private set; }

        private void Start()
        {
            pause = player.GetComponent<PlayerInput>().actions.FindAction("Gameplay/Pause", true);
            pause.performed += RequestPause;
            progression.StageCompleted += StageCompleted;

            GameSettings.ApplyToScene();
            previewSource = gameObject.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
            previewSource.spatialBlend = 0f;
            previewSource.ignoreListenerPause = true;

            ShowTitle();
        }

        private void OnDestroy()
        {
            if (pause != null)
                pause.performed -= RequestPause;
            progression.StageCompleted -= StageCompleted;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameSettings.Save();
        }

        private void Update()
        {
            if (pauseRequested)
            {
                pauseRequested = false;
                if (starting)
                    return;
                switch (screen)
                {
                    case Screen.Playing: SetScreen(Screen.Paused); break;
                    case Screen.Paused: SetScreen(Screen.Playing); break;
                    case Screen.Settings: SetScreen(settingsReturn); break;
                    case Screen.Brightness: SetScreen(Screen.Settings); break;
                }
            }
        }

        // The player controller grabs the cursor on start and on clicks; keep it free while a menu is up.
        private void LateUpdate()
        {
            if (screen != Screen.Playing && Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void RequestPause(InputAction.CallbackContext context) => pauseRequested = true;

        private void ShowTitle()
        {
            screen = Screen.Title;
            Time.timeScale = 0f;
            AudioListener.pause = false; // the house hums quietly behind the title
            player.SetInputBlocked(true);
            if (backdrop != null)
                backdrop.Begin();
        }

        private void SetScreen(Screen next)
        {
            if (next == Screen.Settings && screen is Screen.Title or Screen.Paused)
                settingsReturn = screen;
            screen = next;

            bool playing = next == Screen.Playing;
            if (playing && backdrop != null)
                backdrop.End(); // back to the player's own view before input returns
            bool inGameMenu = next == Screen.Paused || (next is Screen.Settings or Screen.Brightness && settingsReturn == Screen.Paused);
            Time.timeScale = playing ? 1f : 0f;
            AudioListener.pause = inGameMenu;
            player.SetInputBlocked(!playing);
        }

        // START: the title fades to black, the game takes over in the dark, then fades in.
        private IEnumerator StartGame()
        {
            starting = true;
            while (curtain < 1f)
            {
                curtain = startFadeOut > 0f ? Mathf.MoveTowards(curtain, 1f, Time.unscaledDeltaTime / startFadeOut) : 1f;
                yield return null;
            }
            SetScreen(Screen.Playing);
            player.SetInputBlocked(true); // still dark: no walking off before the picture is back

            if (!string.IsNullOrEmpty(startNotice))
            {
                yield return FadeNotice(1f);
                for (float held = 0f; held < noticeHold && !SkipPressed(); held += Time.unscaledDeltaTime)
                    yield return null;
                yield return FadeNotice(0f);
            }

            yield return new WaitForSecondsRealtime(startHold);
            player.SetInputBlocked(false);
            while (curtain > 0f)
            {
                curtain = startFadeIn > 0f ? Mathf.MoveTowards(curtain, 0f, Time.unscaledDeltaTime / startFadeIn) : 0f;
                yield return null;
            }
            starting = false;
        }

        private IEnumerator FadeNotice(float target)
        {
            while (!Mathf.Approximately(noticeAlpha, target))
            {
                noticeAlpha = noticeFade > 0f ? Mathf.MoveTowards(noticeAlpha, target, Time.unscaledDeltaTime / noticeFade) : target;
                yield return null;
            }
        }

        private static bool SkipPressed() =>
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

        private void StageCompleted(int stage)
        {
            if (stage != 6 || IsEnding)
                return;

            IsEnding = true;
            screen = Screen.Ending;
            player.SetInputBlocked(true);
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            while (fade < 1f)
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime / 1.65f);
                yield return null;
            }
            ShowEndingText();
        }

        // Ends the run from a scene that has already cut to black: hold the silence, then the demo ending.
        public void EndFromBlackout(float silence)
        {
            if (IsEnding)
                return;
            IsEnding = true;
            screen = Screen.Ending;
            fade = 1f;
            player.SetInputBlocked(true);
            StartCoroutine(HoldThenShowEnding(silence));
        }

        private IEnumerator HoldThenShowEnding(float silence)
        {
            yield return new WaitForSecondsRealtime(silence);
            ShowEndingText();
        }

        private void ShowEndingText()
        {
            EndingShown = true;
            endingShownAt = Time.unscaledTime;
        }

        private static void BackToTitle()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void QuitGame()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ drawing
        // Text-only menus on the left: a spaced Cinzel title with a red R, quiet item labels that brighten,
        // stutter briefly and gain a dash when hovered, thin sliders with a round knob.

        private static readonly Color Ink = new(.86f, .84f, .8f);
        private static readonly Color Blood = new(.62f, .09f, .07f);
        private const float ItemHeight = 44f;

        private int hovered = -1;
        private float hoverStarted;
        private int itemId;

        private void OnGUI()
        {
            if (screen == Screen.Playing)
            {
                DrawCurtain();
                return;
            }
            CreateStyles();
            itemId = 0;

            int previousDepth = GUI.depth;
            GUI.depth = -100;
            float shade = screen switch
            {
                Screen.Title => .2f, // the live hallway stays visible; the left side is darkened below
                Screen.Ending => fade,
                Screen.Settings or Screen.Brightness when settingsReturn == Screen.Title => .6f,
                _ => .55f,
            };
            Fill(new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height), new Color(0f, 0f, 0f, shade));
            if (screen != Screen.Ending || EndingShown)
                LeftGradient(screen == Screen.Title ? .82f : .9f, screen == Screen.Title ? .6f : .72f);

            switch (screen)
            {
                case Screen.Title: DrawTitle(); break;
                case Screen.Paused: DrawPause(); break;
                case Screen.Settings: DrawSettings(); break;
                case Screen.Brightness: DrawBrightness(); break;
                case Screen.Ending when EndingShown: DrawEnding(); break;
            }
            DrawCurtain();
            GUI.depth = previousDepth;
        }

        private void DrawCurtain()
        {
            if (curtain <= 0f)
                return;
            int previousDepth = GUI.depth;
            GUI.depth = -200;
            Fill(new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height), new Color(0f, 0f, 0f, curtain));
            if (noticeAlpha > 0f)
            {
                CreateStyles();
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, noticeAlpha);
                float mid = UnityEngine.Screen.height * .5f;
                GUI.Label(new Rect(0f, mid - 20f, UnityEngine.Screen.width, 40f), startNotice, noticeStyle);
                GUI.color = new Color(1f, 1f, 1f, noticeAlpha * .5f);
                GUI.Label(new Rect(0f, mid + 34f, UnityEngine.Screen.width, 26f), "아무 키나 눌러 건너뛰기", hintStyle);
                GUI.color = previous;
            }
            GUI.depth = previousDepth;
        }

        private static float Left => Mathf.Max(32f, UnityEngine.Screen.width * .08f);

        private void DrawTitle()
        {
            float top = UnityEngine.Screen.height * .5f - 170f;
            DrawGameTitle(Left, top);
            float y = top + 150f;
            if (Item(ref y, "시작") && !starting)
                StartCoroutine(StartGame());
            if (Item(ref y, "설정") && !starting)
                SetScreen(Screen.Settings);
            if (Item(ref y, "종료") && !starting)
                QuitGame();
        }

        private void DrawPause()
        {
            float y = UnityEngine.Screen.height * .5f - 150f;
            Heading(ref y, "PAUSED");
            if (Item(ref y, T("계속하기", "Resume")))
                SetScreen(Screen.Playing);
            if (Item(ref y, T("설정", "Settings")))
                SetScreen(Screen.Settings);
            if (Item(ref y, T("타이틀로 돌아가기", "Back to Title")))
                BackToTitle();
            if (Item(ref y, T("게임 종료", "Quit Game")))
                QuitGame();
        }

        // Demo-version close, fading in one block after another: the logo, the project and a thank-you,
        // the credit, then the way out.
        private void DrawEnding()
        {
            float since = Time.unscaledTime - endingShownAt;
            Color previous = GUI.color;
            float width = Mathf.Min(620f, UnityEngine.Screen.width - Left - 32f);
            float top = UnityEngine.Screen.height * .5f - 220f;

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(since / 1.6f));
            DrawGameTitle(Left, top);

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((since - 1.2f) / 1.2f));
            float y = top + titleStyle.fontSize * 1.32f + 34f;
            GUI.Label(new Rect(Left, y, width, 30f), "증강현실과 가상현실 텀프로젝트", subtitleStyle);
            GUI.Label(new Rect(Left, y + 34f, width, 28f), "데모 버전을 플레이해주셔서 감사합니다.", labelStyle);

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((since - 2.2f) / 1.2f));
            y += 104f;
            Spaced(Left, y, "DEVELOPED BY", creditStyle, new Color(Ink.r, Ink.g, Ink.b, .55f), creditStyle.fontSize * .3f);
            GUI.Label(new Rect(Left, y + 24f, width, 28f), "울산대 IT융합전공 20211999 이종범", labelStyle);

            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((since - 3.2f) / .8f));
            y += 92f;
            if (since > 3.2f)
            {
                if (Item(ref y, "타이틀로"))
                    BackToTitle();
                if (Item(ref y, "게임 종료"))
                    QuitGame();
            }
            GUI.color = previous;
        }

        private void DrawSettings()
        {
            float y = UnityEngine.Screen.height * .5f - 210f;
            Heading(ref y, "SETTINGS");

            float master = Slider(ref y, T("전체 볼륨", "Master Volume"), GameSettings.MasterVolume, 0f, 1f, Percent);
            if (!Mathf.Approximately(master, GameSettings.MasterVolume))
                GameSettings.SetMasterVolume(master);

            float ambience = Slider(ref y, T("소리", "Sound"), GameSettings.AmbienceVolume, 0f, 1f, Percent);
            if (!Mathf.Approximately(ambience, GameSettings.AmbienceVolume))
                GameSettings.SetAmbienceVolume(ambience);

            float effects = Slider(ref y, T("효과음", "Effects"), GameSettings.EffectsVolume, 0f, 1f, Percent);
            if (!Mathf.Approximately(effects, GameSettings.EffectsVolume))
            {
                GameSettings.SetEffectsVolume(effects);
                PreviewEffects();
            }

            y += 18f;
            if (Item(ref y, T($"어두움 조절    {Signed(GameSettings.Brightness)}", $"Brightness    {Signed(GameSettings.Brightness)}")))
                SetScreen(Screen.Brightness);
            if (Item(ref y, T("뒤로", "Back")))
            {
                GameSettings.Save();
                SetScreen(settingsReturn);
            }
        }

        // A calibration card: three marks at different darkness, previewed with the chosen exposure.
        private void DrawBrightness()
        {
            float y = UnityEngine.Screen.height * .5f - 240f;
            Heading(ref y, "BRIGHTNESS");

            float width = Mathf.Min(520f, UnityEngine.Screen.width - Left * 2f);
            var card = new Rect(Left, y, width, 210f);
            Fill(card, Color.black);
            float gain = Mathf.Pow(2f, GameSettings.Brightness);
            float[] levels = { .0035f, .012f, .04f }; // linear luminance of each mark before exposure
            float markSize = 86f, gap = (card.width - markSize * 3f) / 4f;
            for (int i = 0; i < levels.Length; i++)
            {
                float v = Mathf.LinearToGammaSpace(Mathf.Clamp01(levels[i] * gain));
                Fill(new Rect(card.x + gap + i * (markSize + gap), card.y + 30f, markSize, markSize), new Color(v, v, v * 1.05f, 1f));
            }
            GUI.Label(new Rect(card.x, card.y + 132f, card.width, 64f),
                T("왼쪽 그림이 겨우 보이고\n가운데 그림이 또렷하게 보이도록 맞추세요.",
                  "Set it so the left mark is barely visible\nand the middle one is clear."), hintStyle);

            y = card.yMax + 26f;
            float brightness = Slider(ref y, T("밝기", "Brightness"), GameSettings.Brightness,
                GameSettings.MinBrightness, GameSettings.MaxBrightness, Signed);
            if (!Mathf.Approximately(brightness, GameSettings.Brightness))
                GameSettings.SetBrightness(brightness);

            y += 10f;
            float rowY = y;
            if (Item(ref y, T("기본값", "Default"), false, Left))
                GameSettings.SetBrightness(0f);
            y = rowY;
            if (Item(ref y, T("확인", "Done"), false, Left + 170f))
            {
                GameSettings.Save();
                SetScreen(Screen.Settings);
            }
        }

        private void PreviewEffects()
        {
            if (effectsPreview == null || Time.unscaledTime - lastPreview < .25f)
                return;
            lastPreview = Time.unscaledTime;
            previewSource.PlayOneShot(effectsPreview, GameSettings.EffectsVolume);
        }

        private void PlayUi(AudioClip clip, float volume)
        {
            if (clip != null)
                previewSource.PlayOneShot(clip, volume * GameSettings.EffectsVolume);
        }

        // ------------------------------------------------------------------ widgets

        // "P R O J E C T  R" in Cinzel, the last letter in dried-blood red, with a faint line underneath.
        private void DrawGameTitle(float x, float y)
        {
            float spacing = titleStyle.fontSize * .16f;
            x = Spaced(x, y, "PROJECT", titleStyle, Ink, spacing);
            x += titleStyle.fontSize * .45f;
            x = Spaced(x, y, "R", titleStyle, Blood, spacing);
            x += titleStyle.fontSize * .4f;
            Spaced(x, y + titleStyle.fontSize * .62f, "DEMO", creditStyle, new Color(Blood.r, Blood.g, Blood.b, .9f), creditStyle.fontSize * .45f);
            Fill(new Rect(Left + 2f, y + titleStyle.fontSize * 1.32f, 140f, 1f), new Color(Ink.r, Ink.g, Ink.b, .28f));
        }

        private void Heading(ref float y, string text)
        {
            Spaced(Left, y, text, headingStyle, Ink, headingStyle.fontSize * .18f);
            Fill(new Rect(Left + 2f, y + headingStyle.fontSize * 1.45f, 90f, 1f), new Color(Ink.r, Ink.g, Ink.b, .25f));
            y += headingStyle.fontSize * 2.4f;
        }

        // Draws text one letter at a time so it can be letter-spaced; returns the x after the last letter.
        private static float Spaced(float x, float y, string text, GUIStyle style, Color color, float spacing)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = color;
            foreach (char c in text)
            {
                var content = new GUIContent(c.ToString());
                Vector2 size = style.CalcSize(content);
                GUI.Label(new Rect(x, y, size.x + 4f, size.y), content, style);
                x += size.x + spacing;
            }
            GUI.contentColor = previous;
            return x;
        }

        // A text-only menu entry. Hovering brightens it, makes it stutter for a moment like the lamps,
        // shows a dash and ticks; clicking knocks.
        private bool Item(ref float y, string text, bool spaced = false, float? x = null)
        {
            int id = itemId++;
            float left = x ?? Left;
            GUIStyle style = spaced ? itemSpacedStyle : itemStyle;
            float width = spaced ? style.fontSize * text.Length * 1.05f + 40f : style.CalcSize(new GUIContent(text)).x + 40f;
            var rect = new Rect(left, y, width, ItemHeight);
            y += ItemHeight + 6f;

            bool over = rect.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
            {
                if (over && hovered != id)
                {
                    hovered = id;
                    hoverStarted = Time.unscaledTime;
                    PlayUi(uiHover, .5f);
                }
                else if (!over && hovered == id)
                {
                    hovered = -1;
                }

                float alpha = .58f;
                if (over)
                {
                    float since = Time.unscaledTime - hoverStarted;
                    alpha = since < .14f ? (Random.value < .5f ? .35f : 1f) : 1f; // brief stutter
                    Fill(new Rect(left - 26f, rect.y + ItemHeight * .5f, 16f, 1.5f), new Color(Ink.r, Ink.g, Ink.b, alpha));
                }
                var color = new Color(Ink.r, Ink.g, Ink.b, alpha);
                if (spaced)
                    Spaced(left, rect.y + (ItemHeight - style.fontSize * 1.35f) * .5f, text, style, color, 4f);
                else
                {
                    Color previous = GUI.contentColor;
                    GUI.contentColor = color;
                    GUI.Label(rect, text, style);
                    GUI.contentColor = previous;
                }
            }

            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                PlayUi(uiSelect, .9f);
                return true;
            }
            return false;
        }

        private float Slider(ref float y, string label, float value, float min, float max, System.Func<float, string> format)
        {
            float width = Mathf.Min(460f, UnityEngine.Screen.width - Left * 2f);
            GUI.Label(new Rect(Left, y, width * .32f, 32f), label, labelStyle);

            var track = new Rect(Left + width * .34f, y + 16f, width * .5f, 1.5f);
            float t = Mathf.InverseLerp(min, max, value);
            Fill(track, new Color(Ink.r, Ink.g, Ink.b, .22f));
            Fill(new Rect(track.x, track.y, track.width * t, track.height), new Color(Ink.r, Ink.g, Ink.b, .75f));
            float result = GUI.HorizontalSlider(new Rect(track.x, y + 9f, track.width, 16f), value, min, max, sliderStyle, thumbStyle);

            GUI.Label(new Rect(Left + width * .86f, y, width * .14f, 32f), format(result), valueStyle);
            y += 48f;
            return result;
        }

        private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
        private static string Signed(float v) => v >= 0f ? $"+{v:0.0}" : $"{v:0.0}";

        private string T(string ko, string en) => korean ? ko : en;

        // Dark on the left edge, fading to nothing at `reach` of the screen width.
        private static void LeftGradient(float alpha, float reach)
        {
            const int strips = 48;
            float total = UnityEngine.Screen.width * reach, strip = total / strips;
            for (int i = 0; i < strips; i++)
            {
                float a = alpha * (1f - (float)i / strips);
                Fill(new Rect(i * strip, 0f, strip + 1f, UnityEngine.Screen.height), new Color(0f, 0f, 0f, a * a / alpha));
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color * previous; // keeps any fade set around the call
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void CreateStyles()
        {
            if (titleStyle != null)
                return;

            Font ui = uiFont;
            if (ui == null)
                ui = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR" }, 18);
            korean = ui != null && ui.HasCharacter('설') && ui.HasCharacter('정');
            if (!korean)
                ui = GUI.skin.font;
            Font display = titleFont != null ? titleFont : ui;

            titleStyle = new GUIStyle(GUI.skin.label) { font = display, fontSize = 66, alignment = TextAnchor.UpperLeft, wordWrap = false };
            titleStyle.normal.textColor = Color.white;
            headingStyle = new GUIStyle(titleStyle) { fontSize = 30 };

            itemStyle = new GUIStyle(GUI.skin.label) { font = ui, fontSize = 20, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            itemStyle.normal.textColor = Color.white;
            itemSpacedStyle = new GUIStyle(itemStyle) { alignment = TextAnchor.UpperLeft };

            labelStyle = new GUIStyle(GUI.skin.label) { font = ui, fontSize = 17, alignment = TextAnchor.MiddleLeft };
            labelStyle.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, .8f);
            valueStyle = new GUIStyle(labelStyle) { fontSize = 15, alignment = TextAnchor.MiddleRight };
            valueStyle.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, .55f);
            hintStyle = new GUIStyle(labelStyle) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(.6f, .6f, .58f);
            subtitleStyle = new GUIStyle(labelStyle) { fontSize = 20 };
            subtitleStyle.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, .95f);
            creditStyle = new GUIStyle(titleStyle) { fontSize = 15 }; // small capitals in the title face
            noticeStyle = new GUIStyle(labelStyle) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
            noticeStyle.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, .92f);

            sliderStyle = new GUIStyle { fixedHeight = 16f };
            thumbStyle = new GUIStyle { fixedWidth = 14f, fixedHeight = 14f, margin = new RectOffset(0, 0, 1, 0) };
            thumbStyle.normal.background = Circle(new Color(Ink.r, Ink.g, Ink.b, .9f));
            thumbStyle.hover.background = Circle(Color.white);
            thumbStyle.active.background = Circle(Color.white);
        }

        private static Texture2D Circle(Color color)
        {
            const int size = 28;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            float r = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(r, r));
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(r - d)));
                }
            texture.Apply();
            return texture;
        }
    }
}
