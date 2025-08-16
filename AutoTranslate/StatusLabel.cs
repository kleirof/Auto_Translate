using System;
using System.Collections;
using ETGGUI;
using SGUI;
using UnityEngine;

namespace AutoTranslate
{
    public class StatusLabelController
    {
        internal SLabel label;
        private Font font;
        private readonly Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        private readonly Color defaultColor = Color.white;
        private readonly Color highlightColor = Color.red;

        private StatusLabelModifier modifier;

        private bool highlight = false;

        private AutoTranslateConfig config;

        private readonly Vector2 defaultAnchor = new Vector2(0.5f, 0f);
        private readonly Vector2 defaultPivot = new Vector2(0.5f, 0f);

        private Vector2 anchor;
        private Vector2 pivot;

        public static StatusLabelController instance;

        internal void InitializeStatusLabel()
        {
            instance = this;
            config = AutoTranslateModule.instance.config;

            try
            {
                anchor = ParseVector2(config.CountLabelAnchor);
                pivot = ParseVector2(config.CountLabelPivot);
            }
            catch
            {
                anchor = defaultAnchor;
                pivot = defaultPivot;
            }
            label = new SLabel();
            label.Background = backgroundColor;
            label.Foreground = defaultColor;
            modifier = new StatusLabelModifier(config, this);
            label.With.Add(modifier);
            label.OnUpdateStyle = (element =>
            {
                element.Font = LoadFont();
                Reposition();
            });
            label.Visible = config.ShowRequestedCharacterCount;
            SGUIRoot.Main.Children.Add(label);
        }

        private Font LoadFont()
        {
            if (font == null)
            {
                dfFont font = (dfFont)GameUIRoot.Instance.Manager.DefaultFont;
                this.font = FontConverter.GetFontFromdfFont(font, 2);
            }
            return font;
        }

        internal void SetText(string text)
        {
            label.Text = text;
            label.Size = label.Backend.MeasureText(text, null, font);
            Reposition();
        }

        private void Reposition()
        {
            if (label.Root != null)
            {
                label.Position.x = label.Root.Size.x * anchor.x - label.Size.x * pivot.x;
                label.Position.y = label.Root.Size.y * anchor.y - label.Size.y * pivot.y;
            }
        }

        internal void SetHighlight()
        {
            if (!highlight)
            {
                highlight = true;
                ForceSetVisibility(true);
                TranslationManager.instance.StartCoroutine(AlternateColor());
            }
        }

        private IEnumerator AlternateColor()
        {
            for (; ; )
            {
                label.Foreground = highlightColor;
                yield return new WaitForSecondsRealtime(0.5f);
                label.Foreground = defaultColor;
                yield return new WaitForSecondsRealtime(0.5f);
            }
        }

        internal void ForceSetVisibility(bool value)
        {
            label.Visible = value;
            Debug.Log($"请求字符计数强制设置为{(value ? "ON" : "OFF")}。RequestedCharacterCount forcefully set to {(value ? "ON" : "OFF")}.");
        }

        internal void ToggleVisibility()
        {
            label.Visible = !label.Visible;
            Debug.Log($"请求字符计数切换为{(label.Visible ? "ON" : "OFF")}。RequestedCharacterCount toggled set to {(label.Visible ? "ON" : "OFF")}.");
        }

        public static Vector2 ParseVector2(string input)
        {
            if (input == null)
                throw new ArgumentException("输入不能为空。Input cannot be empty.");

            int len = input.Length;
            int i = 0;

            while (i < len && char.IsWhiteSpace(input[i])) i++;
            if (i >= len) throw new FormatException("输入不能为空。Input cannot be empty.");

            int start = i;
            if (input[i] == '-' || input[i] == '+') i++;
            bool hasDot = false;
            while (i < len)
            {
                char c = input[i];
                if (char.IsDigit(c)) { i++; }
                else if (c == '.' && !hasDot) { hasDot = true; i++; }
                else break;
            }
            if (i == start || (i == start + 1 && (input[start] == '-' || input[start] == '+')))
                throw new FormatException("第一个数字不能为空。First number cannot be empty.");

            float x;
            if (!float.TryParse(input.Substring(start, i - start), out x))
                throw new FormatException("无法解析第一个数字。Failed to parse the first number.");

            while (i < len && char.IsWhiteSpace(input[i])) i++;

            if (i >= len || (input[i] != ',' && !char.IsWhiteSpace(input[i])))
                throw new FormatException("输入格式不正确，应该是两个数字用空格或逗号分隔。The input format is incorrect.");
            i++;

            while (i < len && char.IsWhiteSpace(input[i])) i++;
            if (i >= len) throw new FormatException("第二个数字不能为空。Second number cannot be empty.");

            start = i;
            if (input[i] == '-' || input[i] == '+') i++;
            hasDot = false;
            while (i < len)
            {
                char c = input[i];
                if (char.IsDigit(c)) { i++; }
                else if (c == '.' && !hasDot) { hasDot = true; i++; }
                else break;
            }
            if (i == start || (i == start + 1 && (input[start] == '-' || input[start] == '+')))
                throw new FormatException("第二个数字不能为空。Second number cannot be empty.");

            float y;
            if (!float.TryParse(input.Substring(start, i - start), out y))
                throw new FormatException("无法解析第二个数字。Failed to parse the second number.");

            while (i < len && char.IsWhiteSpace(input[i])) i++;
            if (i != len)
                throw new FormatException("输入格式不正确，存在多余字符。Extra characters found.");

            return new Vector2(x, y);
        }

        internal class StatusLabelModifier : SModifier
        {
            private AutoTranslateConfig config;
            private StatusLabelController statusLabel;

            public StatusLabelModifier(AutoTranslateConfig autoTranslateConfig, StatusLabelController statusLabelController)
            {
                config = autoTranslateConfig;
                statusLabel = statusLabelController;
            }

            public override void Update()
            {
                if (Input.GetKeyDown(config.ToggleRequestedCharacterCountKeyBinding))
                {
                    statusLabel.ToggleVisibility();
                }
            }
        }
    }
}
