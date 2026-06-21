using UnityEngine;

namespace CheatMenu
{
    public partial class Plugin
    {
        // ════════════════════════════════════════════
        //  样式构建 — 每帧重建皮肤，完全独立于游戏
        // ════════════════════════════════════════════

        private GUISkin BuildSkin()
        {
            var skin = ScriptableObject.CreateInstance<GUISkin>();
            skin.hideFlags = HideFlags.HideAndDontSave;

            // 字体：优先用缓存的游戏默认字体，其次系统字体，最后 Arial
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16)
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            skin.font = _font;

            Color accent = new Color(0.35f, 0.6f, 0.95f, 1f);

            // ── Window ──
            skin.window = new GUIStyle
            {
                name = "CheatWindow",
                normal =
                {
                    background = MakeBorderTex(new Color(0.10f, 0.10f, 0.13f, 0.98f), new Color(0.30f, 0.45f, 0.70f, 0.9f)),
                    textColor = new Color(0.8f, 0.8f, 0.9f)
                },
                onNormal =
                {
                    background = MakeBorderTex(new Color(0.10f, 0.10f, 0.13f, 0.98f), new Color(0.30f, 0.45f, 0.70f, 0.9f))
                },
                border = new RectOffset(2, 2, 2, 2),
                fontSize = 16,
                padding = new RectOffset(12, 12, 10, 12)
            };

            // ── Box ──
            skin.box = new GUIStyle
            {
                name = "CheatBox",
                normal =
                {
                    background = MakeBorderTex(new Color(0.13f, 0.13f, 0.17f, 0.95f), new Color(0.25f, 0.25f, 0.35f, 0.6f)),
                    textColor = new Color(0.7f, 0.7f, 0.75f)
                },
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(16, 16, 12, 12)
            };

            // ── Button ──
            skin.button = new GUIStyle
            {
                name = "CheatButton",
                normal =
                {
                    background = MakeBorderTex(new Color(0.28f, 0.30f, 0.38f, 1f), new Color(0.50f, 0.60f, 0.85f, 0.9f)),
                    textColor = new Color(0.90f, 0.90f, 0.95f)
                },
                hover =
                {
                    background = MakeBorderTex(new Color(0.35f, 0.38f, 0.48f, 1f), new Color(0.55f, 0.70f, 1f, 1f)),
                    textColor = Color.white
                },
                active =
                {
                    background = MakeBorderTex(new Color(0.30f, 0.45f, 0.70f, 0.8f), accent),
                    textColor = Color.white
                },
                border = new RectOffset(2, 2, 2, 2),
                fontSize = 15,
                padding = new RectOffset(14, 14, 8, 8),
                margin = new RectOffset(4, 4, 4, 4)
            };

            // ── Toggle（从默认皮肤复制勾选框纹理，仅覆盖文字色） ──
            var defToggle = new GUIStyle(_defaultSkin.toggle);
            skin.toggle = new GUIStyle(defToggle)
            {
                name = "CheatToggle",
                normal =
                {
                    background = defToggle.normal.background,
                    textColor = new Color(0.75f, 0.75f, 0.80f)
                },
                onNormal =
                {
                    background = defToggle.onNormal.background,
                    textColor = new Color(0.85f, 0.85f, 0.90f)
                },
                hover =
                {
                    background = defToggle.hover.background,
                    textColor = Color.white
                },
                onHover =
                {
                    background = defToggle.onHover.background,
                    textColor = Color.white
                },
                active =
                {
                    background = defToggle.active.background,
                },
                onActive =
                {
                    background = defToggle.onActive.background,
                },
                focused =
                {
                    background = defToggle.focused.background,
                },
                onFocused =
                {
                    background = defToggle.onFocused.background,
                },
                fontSize = 15,
                padding = new RectOffset(26, 8, 6, 6),
                margin = new RectOffset(6, 6, 4, 4)
            };

            // ── TextField ──
            skin.textField = new GUIStyle
            {
                name = "CheatField",
                normal =
                {
                    background = MakeBorderTex(new Color(0.14f, 0.14f, 0.18f, 1f), new Color(0.45f, 0.55f, 0.80f, 0.7f)),
                    textColor = Color.white
                },
                focused =
                {
                    background = MakeBorderTex(new Color(0.14f, 0.14f, 0.18f, 1f), new Color(0.45f, 0.55f, 0.80f, 0.7f)),
                    textColor = Color.white
                },
                border = new RectOffset(2, 2, 2, 2),
                fontSize = 15,
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(4, 4, 4, 4)
            };

            // ── Label ──
            skin.label = new GUIStyle
            {
                name = "CheatLabel",
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f) },
                fontSize = 15,
                padding = new RectOffset(0, 0, 4, 4)
            };

            // ── HorizontalSlider ──
            skin.horizontalSlider = new GUIStyle
            {
                name = "CheatHSlider",
                normal = { background = MakeBorderTex(new Color(0.20f, 0.20f, 0.25f, 1f), new Color(0.35f, 0.35f, 0.45f, 0.8f)) },
                fixedHeight = 12,
                border = new RectOffset(1, 1, 1, 1)
            };

            skin.horizontalSliderThumb = new GUIStyle
            {
                name = "CheatHSliderThumb",
                normal = { background = MakeBorderTex(accent, accent * 1.2f) },
                hover = { background = MakeBorderTex(accent * 1.1f, Color.white) },
                active = { background = MakeBorderTex(accent * 0.9f, Color.white) },
                fixedHeight = 16,
                fixedWidth = 16
            };

            // ── ScrollView ──
            skin.scrollView = new GUIStyle();

            // ── 自定义样式 ──
            _boxStyle = new GUIStyle(skin.box) { padding = new RectOffset(16, 16, 12, 12) };
            _btnStyle = skin.button;
            _toggleStyle = skin.toggle;
            _fieldStyle = skin.textField;

            _titleStyle = new GUIStyle(skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = accent },
                padding = new RectOffset(0, 0, 12, 8)
            };

            _labelStyle = new GUIStyle(skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f) },
                padding = new RectOffset(0, 0, 4, 4)
            };

            _statLabel = new GUIStyle(skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.70f, 0.82f, 1f) },
                padding = new RectOffset(0, 0, 4, 4)
            };

            _smallLabel = new GUIStyle(skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.50f, 0.50f, 0.55f) }
            };

            return skin;
        }

        /// <summary>
        /// 5×5 带边框纹理，外圈 borderColor，内圈 bgColor。
        /// </summary>
        private Texture2D MakeBorderTex(Color bgColor, Color borderColor)
        {
            int s = 5;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = (x == 0 || x == s - 1 || y == 0 || y == s - 1) ? borderColor : bgColor;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
