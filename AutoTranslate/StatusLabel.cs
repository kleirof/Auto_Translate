using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ETGGUI;
using SGUI;
using UnityEngine;

namespace AutoTranslate
{
    public class StatusLabelController
    {
        internal SLabel label;
        private Font defaultFont;
        private readonly Color backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        private readonly Color defaultColor = Color.white;
        private readonly Color highlightColor = Color.red;

        private StatusLabelModifier modifier;

        private bool highlight = false;

        private AutoTranslateConfig config;

        public static StatusLabelController instance;

        internal void InitializeStatusLabel()
        {
            instance = this;
            config = AutoTranslateModule.instance.config;
            label = new SLabel();
            label.Background = backgroundColor;
            label.Foreground = defaultColor;
            modifier = new StatusLabelModifier(config);
            label.With.Add(modifier);
            label.OnUpdateStyle = (element =>
            {
                element.Font = LoadFont();
                if (label.Root != null)
                {
                    label.Position.x = label.Root.Size.x - label.Size.x;
                    label.Position.y = label.Root.Size.y * 0.16f;
                }
            });
            label.Visible = config.ShowRequestedCharacterCount;
            SGUIRoot.Main.Children.Add(label);
        }

        private Font LoadFont()
        {
            if (defaultFont == null)
            {
                dfFont font = (dfFont)GameUIRoot.Instance.Manager.DefaultFont;
                defaultFont = FontConverter.GetFontFromdfFont(font, 2);
            }
            return defaultFont;
        }

        internal void SetText(string text)
        {
            label.Text = text;
            label.Size = label.Backend.MeasureText(text, null, defaultFont);
            label.Position.x = label.Root.Size.x - label.Size.x;
        }

        internal void SetHighlight()
        {
            if (!highlight)
            {
                highlight = true;
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

        internal class StatusLabelModifier : SModifier
        {
            private AutoTranslateConfig config;

            public StatusLabelModifier(AutoTranslateConfig autoTranslateConfig)
            {
                config = autoTranslateConfig;
            }

            public override void Update()
            {
                if (Input.GetKeyDown(config.ToggleRequestedCharacterCountKeyBinding))
                {
                    Elem.Visible = !Elem.Visible;
                    Debug.Log($"RequestedCharacterCount toggled to {(Elem.Visible ? "ON" : "OFF")}.");
                }
            }
        }
    }
}
