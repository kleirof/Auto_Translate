using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private static readonly Regex vector2Regex = new Regex(@"^\s*(-?\d+(\.\d+)?)\s*[, ]\s*(-?\d+(\.\d+)?)\s*$");

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

        private static bool IsNullOrWhiteSpace(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }

        public static Vector2 ParseVector2(string input)
        {
            if (IsNullOrWhiteSpace(input))
                throw new ArgumentException("输入不能为空。Input cannot be empty.");

            Match match = vector2Regex.Match(input);
            if (!match.Success)
                throw new FormatException("输入格式不正确，应该是两个数字用空格或逗号分隔。The input format is incorrect. It should be two numbers separated by a space or comma.");

            float x = float.Parse(match.Groups[1].Value);
            float y = float.Parse(match.Groups[3].Value);

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
