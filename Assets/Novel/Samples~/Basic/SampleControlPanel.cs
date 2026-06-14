#nullable enable
using Novel.View;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Novel.Samples
{
    // novel-kit の再生制御（Auto/Skip）をテストする簡易ボタンパネル。
    // ボタンは ChoiceButton prefab を流用し Canvas 左上に動的生成、状態をラベルへ反映する。
    public sealed class SampleControlPanel : MonoBehaviour
    {
        [SerializeField] private NovelMessageView view = null!;
        [SerializeField] private Button buttonPrefab = null!;
        [SerializeField] private Canvas canvas = null!;

        private TMP_Text? _autoLabel;
        private TMP_Text? _skipLabel;

        private void Start()
        {
            _autoLabel = CreateButton(0, () => view.ToggleAuto());
            _skipLabel = CreateButton(1, () => view.ToggleSkip());
        }

        private void Update()
        {
            if (_autoLabel != null) _autoLabel.text = view.IsAuto ? "Auto: ON" : "Auto: OFF";
            if (_skipLabel != null) _skipLabel.text = view.IsSkip ? "Skip: ON" : "Skip: OFF";
        }

        private TMP_Text? CreateButton(int index, UnityAction onClick)
        {
            var button = Instantiate(buttonPrefab, canvas.transform);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -20f - index * 80f);
            rt.sizeDelta = new Vector2(220f, 64f);
            button.onClick.AddListener(onClick);
            return button.GetComponentInChildren<TMP_Text>();
        }
    }
}
