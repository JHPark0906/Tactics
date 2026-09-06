using System;
using HS.Framework.UI.Windows;
using HS.Tactics.Flow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HS.Tactics.Editor
{
    /// <summary>Level1의 실제 결과 창을 만들고 기존 전투 흐름과 창 관리자에 연결한다.</summary>
    public static class BattleResultSceneWiring
    {
        private const string ScenePath = "Assets/_HS/Tactics/Scenes/Level1.unity";

        [MenuItem("Tools/HS Tactics/Battle/Wire Result Window")]
        public static void WireResultWindow()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var flow = FindInScene<StageFlowController>(scene);
            var manager = FindInScene<UiWindowManager>(scene);
            if (flow == null || manager == null)
            {
                throw new InvalidOperationException("Level1의 StageFlowController와 UiWindowManager가 필요하다.");
            }

            var serializedFlow = new SerializedObject(flow);
            var window = serializedFlow.FindProperty("resultWindow").objectReferenceValue as BattleResultWindow
                         ?? FindInScene<BattleResultWindow>(scene);
            if (window == null)
            {
                var root = new GameObject("BattleResultWindow", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                window = root.AddComponent<BattleResultWindow>();
            }

            var backdrop = EnsureChild<Image>(window.transform, "Backdrop");
            Stretch(backdrop.rectTransform);
            backdrop.color = new Color(0.02f, 0.03f, 0.05f, 0.72f);
            var panel = EnsureChild<Image>(backdrop.transform, "Panel");
            Center(panel.rectTransform, Vector2.zero, new Vector2(580f, 320f));
            panel.color = new Color(0.10f, 0.13f, 0.18f, 1f);

            var sceneText = FindInScene<TMP_Text>(scene);
            var font = sceneText != null && sceneText.font != null ? sceneText.font : TMP_Settings.defaultFontAsset;
            var title = EnsureText(panel.transform, "Title", "Victory", font, new Vector2(0f, 93f), 36f);
            var message = EnsureText(panel.transform, "Message", "스테이지를 클리어했다.", font, new Vector2(0f, 18f), 24f);
            var buttonImage = EnsureChild<Image>(panel.transform, "ProceedButton");
            Center(buttonImage.rectTransform, new Vector2(0f, -93f), new Vector2(260f, 58f));
            buttonImage.color = new Color(0.20f, 0.43f, 0.65f, 1f);
            var button = buttonImage.GetComponent<Button>() ?? buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            var label = EnsureText(button.transform, "Label", "스테이지 선택", font, Vector2.zero, 24f);
            Stretch(label.rectTransform);

            var serializedWindow = new SerializedObject(window);
            serializedWindow.FindProperty("titleText").objectReferenceValue = title;
            serializedWindow.FindProperty("messageText").objectReferenceValue = message;
            serializedWindow.FindProperty("proceedButton").objectReferenceValue = button;
            serializedWindow.FindProperty("proceedButtonLabel").objectReferenceValue = label;
            serializedWindow.FindProperty("isModal").boolValue = true;
            serializedWindow.ApplyModifiedPropertiesWithoutUndo();
            serializedFlow.FindProperty("resultWindow").objectReferenceValue = window;
            serializedFlow.FindProperty("windowManager").objectReferenceValue = manager;
            serializedFlow.ApplyModifiedPropertiesWithoutUndo();
            window.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BattleResultSceneWiring] Level1 결과 창과 진행 버튼을 연결했다.");
        }

        private static T EnsureChild<T>(Transform parent, string name) where T : Component
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform)).transform;
                child.SetParent(parent, false);
            }

            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }

        private static TextMeshProUGUI EnsureText(
            Transform parent, string name, string value, TMP_FontAsset font, Vector2 position, float size)
        {
            var text = EnsureChild<TextMeshProUGUI>(parent, name);
            Center(text.rectTransform, position, new Vector2(510f, 72f));
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static T FindInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
