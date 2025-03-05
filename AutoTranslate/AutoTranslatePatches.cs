using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Reflection;
using UnityEngine;
using ETGGUI;
using System.Collections.Generic;
using System.Linq;

namespace AutoTranslate
{
    public static class AutoTranslatePatches
    {
        private static AutoTranslateConfig config = AutoTranslateModule.instance.config;

        public static void EmitCall<T>(this ILCursor iLCursor, string methodName, Type[] parameters = null, Type[] generics = null)
        {
            MethodInfo methodInfo = AccessTools.Method(typeof(T), methodName, parameters, generics);
            iLCursor.Emit(OpCodes.Call, methodInfo);
        }

        public static bool TheNthTime(this Func<bool> predict, int n = 1)
        {
            for (int i = 0; i < n; ++i)
            {
                if (!predict())
                    return false;
            }
            return true;
        }

        public static T GetFieldValueInEnumerator<T>(object instance, string fieldNamePattern)
        {
            return (T)instance.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.Contains("$" + fieldNamePattern) || f.Name.Contains("<" + fieldNamePattern + ">") || f.Name == fieldNamePattern)
                .GetValue(instance);
        }

        private static void DfLabelTextAddCall(dfLabel instance)
        {
            dfFontBase fontBase = FontManager.instance.dfFontBase;
            if (fontBase != null && instance.Font != fontBase)
            {
                instance.Font = fontBase;
                if (fontBase is dfFont dfFont)
                    instance.Atlas = dfFont.Atlas;

                if (config.DfTextScaleExpandThreshold >= 0 && instance.TextScale < config.DfTextScaleExpandThreshold)
                    instance.TextScale = config.DfTextScaleExpandToValue;
            }

            TranslationManager.instance.AddTranslationRequest(instance.text, instance);
        }

        private static void DfButtonTextAddCall(dfButton instance)
        {
            dfFontBase fontBase = FontManager.instance.dfFontBase;
            if (fontBase != null && instance.Font != fontBase)
            {
                instance.Font = fontBase;
                if (fontBase is dfFont dfFont)
                    instance.Atlas = dfFont.Atlas;

                if (config.DfTextScaleExpandThreshold >= 0 && instance.TextScale < config.DfTextScaleExpandThreshold)
                    instance.TextScale = config.DfTextScaleExpandToValue;
            }

            TranslationManager.instance.AddTranslationRequest(instance.text, instance);
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.Text), MethodType.Setter)]
        public class DfLabelTextPatchClass
        {
            [HarmonyPostfix]
            public static void DfLabelTextPostfix(dfLabel __instance)
            {
                DfLabelTextAddCall(__instance);
            }
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.OnLocalize))]
        public class DfLabelOnLocalizePatchClass
        {
            [HarmonyPostfix]
            public static void DfLabelOnLocalizePostfix(dfLabel __instance)
            {
                DfLabelTextAddCall(__instance);
            }
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.ModifyLocalizedText))]
        public class DfLabelModifyLocalizedTextPatchClass
        {
            [HarmonyPostfix]
            public static void DfLabelModifyLocalizedTextPostfix(dfLabel __instance)
            {
                DfLabelTextAddCall(__instance);
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.Text), MethodType.Setter)]
        public class DfButtonTextPatchClass
        {
            [HarmonyPostfix]
            public static void DfButtonTextPostfix(dfButton __instance)
            {
                DfButtonTextAddCall(__instance);
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.OnLocalize))]
        public class DfButtonOnLocalizePatchClass
        {
            [HarmonyPostfix]
            public static void DfButtonOnLocalizePostfix(dfButton __instance)
            {
                DfButtonTextAddCall(__instance);
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.ModifyLocalizedText))]
        public class DfButtonModifyLocalizedTextPatchClass
        {
            [HarmonyPostfix]
            public static void DfButtonModifyLocalizedTextPostfix(dfButton __instance)
            {
                DfButtonTextAddCall(__instance);
            }
        }

        public class GmStartPatchClass
        {
            public static void GmStartPostfix(object __instance)
            {
                FontManager.instance.itemTipsModuleObject = __instance;
                FieldInfo fieldInfo = FontManager.instance.itemTipsModuleType.GetField("_infoLabel", BindingFlags.NonPublic | BindingFlags.Instance);
                FontManager.instance.itemTipsModuleLabel = fieldInfo.GetValue(__instance) as SGUI.SLabel;
            }
        }

        public class ShowTipPatchClass
        {
            public static void ShowTipPostfix()
            {
                if (FontManager.instance.itemTipsModuleText != null)
                    TranslationManager.instance.AddTranslationRequest(FontManager.instance.itemTipsModuleText, FontManager.instance.itemTipsModuleLabel);
            }
        }

        public class LoadFontPatchClass
        {
            public static bool LoadFontPrefix(ref Font __result)
            {
                FieldInfo fieldInfo = FontManager.instance.itemTipsModuleType.GetField("_gameFont", BindingFlags.NonPublic | BindingFlags.Instance);
                dfFontBase fontBase = FontManager.instance.dfFontBase;
                if (fontBase != null && fontBase is dfDynamicFont dynamicFont)
                {
                    __result = dynamicFont.baseFont;
                    fieldInfo.SetValue(FontManager.instance.itemTipsModuleObject, __result);
                    FontManager.instance.itemTipsModuleFont = __result;
                    return false;
                }
                else if (fontBase != null && fontBase is dfFont dfFont)
                {
                    __result = FontConverter.GetFontFromdfFont(dfFont, 2);
                    fieldInfo.SetValue(FontManager.instance.itemTipsModuleObject, __result);
                    FontManager.instance.itemTipsModuleFont = __result;
                    return false;
                }

                dfFont font = FontManager.GetGameFont();
                if (font != null)
                {
                    __result = FontConverter.GetFontFromdfFont(font, 2);
                    fieldInfo.SetValue(FontManager.instance.itemTipsModuleObject, __result);
                    FontManager.instance.itemTipsModuleFont = __result;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(tk2dTextMesh), nameof(tk2dTextMesh.text), MethodType.Setter)]
        public class Tk2dTextMeshTextPatchClass
        {
            [HarmonyPostfix]
            public static void Tk2dTextMeshTextPostfix(tk2dTextMesh __instance)
            {
                tk2dFontData tk2dFont = FontManager.instance.tk2dFont;
                if (tk2dFont != null && __instance.font != tk2dFont)
                {
                    FontManager.SetTextMeshFont(__instance, tk2dFont);
                }
                if (__instance.data != null && __instance.data.text != null)
                {
                    TranslationManager.instance.AddTranslationRequest(__instance.data.text, __instance);
                }
            }
        }

        [HarmonyPatch(typeof(tk2dTextMesh), nameof(tk2dTextMesh.font), MethodType.Setter)]
        public class Tk2dTextMeshFontPatchClass
        {
            [HarmonyPrefix]
            public static bool Tk2dTextMeshFontPrefix(tk2dTextMesh __instance)
            {
                tk2dFontData tk2dFont = FontManager.instance.tk2dFont;
                if (tk2dFont != null && __instance.font != tk2dFont)
                {
                    FontManager.SetTextMeshFont(__instance, tk2dFont);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(TextBoxManager), nameof(TextBoxManager.SetText))]
        public class SetTextPatchClass
        {
            [HarmonyPrefix]
            public static void SetTextPrefix(ref bool instant)
            {
                instant = true;
            }
        }

        public class DynamicFontRendererTokenizePatchClass
        {
            public static bool TokenizePrefix(dfDynamicFont.DynamicFontRenderer __instance, string text)
            {
                try
                {
                    if (__instance.tokens != null)
                    {
                        if (ReferenceEquals(__instance.tokens[0].Source, text))
                        {
                            return false;
                        }
                        __instance.tokens.ReleaseItems();
                        __instance.tokens.Release();
                    }
                    __instance.tokens = FontManager.instance?.Tokenize(text);
                    for (int i = 0; i < __instance.tokens.Count; i++)
                    {
                        __instance.calculateTokenRenderSize(__instance.tokens[i]);
                    }
                }
                finally
                {
                }
                return false;
            }
        }

        public class DynamicFontRendererCalculateLinebreaksPatchClass
        {
            [HarmonyILManipulator]
            public static void CalculateLinebreaksPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (((Func<bool>)(() =>
                    crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(0)
                    ))).TheNthTime(8))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)3);
                    crs.EmitCall<DynamicFontRendererCalculateLinebreaksPatchClass>(nameof(DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_1));
                    crs.Index++;
                }

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(0)
                    ))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)3);
                    crs.EmitCall<DynamicFontRendererCalculateLinebreaksPatchClass>(nameof(DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_1));
                }

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(2)
                    ))
                {
                    crs.EmitCall<DynamicFontRendererCalculateLinebreaksPatchClass>(nameof(DynamicFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_2));
                }
            }

            private static void CalculateLinebreaksPatchCall_1(ref int orig)
            {
                orig--;
            }

            private static int CalculateLinebreaksPatchCall_2(int orig)
            {
                return 2;
            }
        }

        public class BitmappedFontRendererTokenizePatchClass
        {
            public static bool TokenizePrefix(dfFont.BitmappedFontRenderer __instance, string text, ref dfList<dfMarkupToken> __result)
            {
                try
                {
                    if (__instance.tokens != null)
                    {
                        if (ReferenceEquals(__instance.tokens[0].Source, text))
                        {
                            __result = __instance.tokens;
                            return false;
                        }
                        __instance.tokens.ReleaseItems();
                        __instance.tokens.Release();
                    }
                    __instance.tokens = FontManager.instance?.Tokenize(text);
                    for (int i = 0; i < __instance.tokens.Count; i++)
                    {
                        __instance.calculateTokenRenderSize(__instance.tokens[i]);
                    }
                    __result = __instance.tokens;
                }
                finally
                {
                }
                return false;
            }
        }

        public class BitmappedFontRendererCalculateLinebreaksPatchClass
        {
            public static void CalculateLinebreaksPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(7)
                    ))
                {
                    crs.EmitCall<BitmappedFontRendererCalculateLinebreaksPatchClass>(nameof(BitmappedFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_1));
                }

                if (crs.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcI4(2)
                    ))
                {
                    crs.EmitCall<BitmappedFontRendererCalculateLinebreaksPatchClass>(nameof(BitmappedFontRendererCalculateLinebreaksPatchClass.CalculateLinebreaksPatchCall_2));
                }
            }

            private static int CalculateLinebreaksPatchCall_1(int orig)
            {
                return 7;
            }

            private static int CalculateLinebreaksPatchCall_2(int orig)
            {
                return 2;
            }
        }

        [HarmonyPatch(typeof(tk2dTextMesh), nameof(tk2dTextMesh.CheckFontsForLanguage))]
        public class Tk2dTextMeshCheckFontsForLanguagePatchClass
        {
            [HarmonyPrefix]
            public static bool CheckFontsForLanguagePrefix()
            {
                if (FontManager.instance.tk2dFont != null)
                    return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(dfLabel), nameof(dfLabel.CheckFontsForLanguage))]
        public class DfLabelCheckFontsForLanguagePatchClass
        {
            [HarmonyPrefix]
            public static bool CheckFontsForLanguagePrefix()
            {
                if (FontManager.instance.tk2dFont != null)
                    return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(dfButton), nameof(dfButton.CheckFontsForLanguage))]
        public class DfButtonCheckFontsForLanguagePatchClass
        {
            [HarmonyPrefix]
            public static bool CheckFontsForLanguagePrefix()
            {
                if (FontManager.instance.tk2dFont != null)
                    return false;
                return true;
            }
        }

        [HarmonyPatch(typeof(DefaultLabelController), nameof(DefaultLabelController.Trigger), new Type[] { typeof(Transform), typeof(Vector3) })]
        public class DefaultLabelControllerTriggerPatchClass
        {
            [HarmonyPrefix]
            public static void TriggerPrefix(DefaultLabelController __instance)
            {
                __instance.label.AutoSize = true;
            }
        }

        [HarmonyPatch(typeof(SimpleStatLabel), nameof(SimpleStatLabel.Start))]
        public class SimpleStatLabelStartPatchClass
        {
            [HarmonyPostfix]
            public static void StartPostfix(SimpleStatLabel __instance)
            {
                __instance.m_label.autoSize = true;
            }
        }

        [HarmonyPatch(typeof(GameUIRoot), nameof(GameUIRoot.UpdatePlayerConsumables))]
        public class UpdatePlayerConsumablesPatchClass
        {
            [HarmonyPrefix]
            public static void UpdatePlayerConsumablesPrefix(GameUIRoot __instance)
            {
                __instance.p_playerKeyLabel.autoSize = true;
                __instance.p_playerCoinLabel.autoSize = true;
            }
        }

        [HarmonyPatch(typeof(dfDynamicFont.DynamicFontRenderer), nameof(dfDynamicFont.DynamicFontRenderer.renderText))]
        public class RenderTextPatchClass
        {
            [HarmonyILManipulator]
            public static void RenderTextPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchCall<CharacterInfo>("get_maxX")))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)3);
                    crs.EmitCall<RenderTextPatchClass>(nameof(RenderTextPatchClass.RenderTextPatchCall));
                }
            }

            private static int RenderTextPatchCall(int orig, ref CharacterInfo characterInfo)
            {
                return characterInfo.advance;
            }
        }

        [HarmonyPatch(typeof(dfDynamicFont.DynamicFontRenderer), nameof(dfDynamicFont.DynamicFontRenderer.calculateTokenRenderSize))]
        public class CalculateTokenRenderSizePatchClass
        {
            [HarmonyILManipulator]
            public static void CalculateTokenRenderSizePatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchCall<CharacterInfo>("get_maxX")))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)3);
                    crs.EmitCall<CalculateTokenRenderSizePatchClass>(nameof(CalculateTokenRenderSizePatchClass.CalculateTokenRenderSizePatchCall));
                }
            }

            private static int CalculateTokenRenderSizePatchCall(int orig, ref CharacterInfo characterInfo)
            {
                return characterInfo.advance;
            }
        }

        [HarmonyPatch]
        public class GetCharacterWidthsPatchClass
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(dfDynamicFont.DynamicFontRenderer), nameof(dfDynamicFont.DynamicFontRenderer.GetCharacterWidths), new Type[] { typeof(string), typeof(int), typeof(int), typeof(float).MakeByRefType() });
            }

            [HarmonyILManipulator]
            public static void GetCharacterWidthsPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchCall<CharacterInfo>("get_maxX")))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)5);
                    crs.EmitCall<GetCharacterWidthsPatchClass>(nameof(GetCharacterWidthsPatchClass.GetCharacterWidthsPatchCall));
                }
            }

            private static int GetCharacterWidthsPatchCall(int orig, ref CharacterInfo characterInfo)
            {
                return characterInfo.advance;
            }
        }

        public class ConvertStringToFixedWidthLinesPatchClass
        {
            public static bool ConvertStringToFixedWidthLinesPrefix(string text, ref List<string> __result)
            {
                __result = new List<string>() { text };
                return false;
            }
        }

        public class GenerateTextInfoPatchClass
        {
            public static void GenerateTextInfoPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchCall<string>("Join")))
                {
                    crs.Emit(OpCodes.Ldloca_S, (byte)4);
                    crs.EmitCall<GenerateTextInfoPatchClass>(nameof(GenerateTextInfoPatchClass.GenerateTextInfoPatchCall));
                }
            }

            private static string GenerateTextInfoPatchCall(string text, ref Vector2 size)
            {
                FontManager.instance.itemTipsModuleText = text;

                string result = FontManager.instance.WrapText(text, out Vector2 sizeVector);
                size = sizeVector;
                return result;
            }
        }

        [HarmonyPatch(typeof(DefaultLabelController), nameof(DefaultLabelController.Expand_CR), MethodType.Enumerator)]
        public class Expand_CRPatchClass
        {
            [HarmonyILManipulator]
            public static void Expand_CRPatch(ILContext ctx)
            {
                ILCursor crs = new ILCursor(ctx);

                if (crs.TryGotoNext(MoveType.After,
                    x => x.MatchLdfld("DefaultLabelController+<Expand_CR>c__Iterator0", "<targetWidth>__0")))
                {
                    crs.Emit(OpCodes.Ldarg_0);
                    crs.EmitCall<Expand_CRPatchClass>(nameof(Expand_CRPatchClass.Expand_CRPatchCall));
                }
            }

            private static float Expand_CRPatchCall(float orig, object selfObject)
            {
                DefaultLabelController self = GetFieldValueInEnumerator<DefaultLabelController>(selfObject, "this");
                return self.label.Width + 1f;
            }
        }

        [HarmonyPatch(typeof(DefaultLabelController), nameof(DefaultLabelController.UpdateForLanguage))]
        public class DefaultLabelControllerUpdateForLanguagePatchClass
        {
            [HarmonyPrefix]
            public static bool UpdateForLanguagePrefix()
            {
                return false;
            }
        }

        public class NewShopItemControllerPatchClass
        {
            public static void NewShopItemControllerPrefix(DefaultLabelController labelController)
            {
                labelController.label.AutoSize = false;
            }
        }
    }
}