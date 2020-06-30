using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Editor : MonoBehaviour
    {
        public const float RightPanelExpandedWidth = 500f;
        public const float RightPanelCollapsedWidth = 0f;

        public static Editor AddTo(RectTransform transform)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);

            var rows = go.AddComponent<VerticalLayoutGroup>();

            var tabs = ScreenTabs.Create(go.transform, VamPrefabFactory.buttonPrefab);

            var panels = new GameObject();
            panels.transform.SetParent(go.transform, false);

            var panelsGroup = panels.AddComponent<HorizontalLayoutGroup>();
            panelsGroup.spacing = 10f;
            panelsGroup.childControlWidth = true;
            panelsGroup.childForceExpandWidth = false;
            panelsGroup.childControlWidth = true;
            panelsGroup.childForceExpandWidth = false;

            var leftPanel = CreatePanel(panels.transform, 0f, 1f);
            var rightPanel = CreatePanel(panels.transform, RightPanelExpandedWidth, 0f);

            var editor = go.AddComponent<Editor>();
            editor.tabs = tabs;
            editor.leftPanel = leftPanel;
            editor.rightPanel = rightPanel;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return editor;
        }

        private static GameObject CreatePanel(Transform transform, float preferredWidth, float flexibleWidth)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1f);

            var layout = go.AddComponent<LayoutElement>();
            layout.minWidth = 0;
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = flexibleWidth;

            var verticalLayoutGroup = go.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childForceExpandWidth = false;
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.spacing = 10f;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            return go;
        }

        public bool locked
        {
            get { return _controlPanel.locked; }
            set { _controlPanel.locked = value; _screensManager.UpdateLocked(value); }
        }

        public ScreenTabs tabs;
        public GameObject leftPanel;
        public GameObject rightPanel;
        private AnimationControlPanel _controlPanel;
        private IAtomPlugin _plugin;
        private ScreensManager _screensManager;
        private VamPrefabFactory _leftPanelPrefabFactory;
        private Curves _curves;
        private CurveTypePopup _curveType;
        private bool _expanded = true;
        private UIDynamicButton _expandButton;

        public void Bind(IAtomPlugin plugin)
        {
            _plugin = plugin;

            _leftPanelPrefabFactory = leftPanel.AddComponent<VamPrefabFactory>();
            _leftPanelPrefabFactory.plugin = plugin;

            _controlPanel = CreateControlPanel(leftPanel);
            _controlPanel.Bind(plugin);

            InitClipboardUI();

            InitChangeCurveTypeUI();

            _curves = InitCurvesUI();

            InitAutoKeyframeUI();

            tabs.Add(EditScreen.ScreenName);
            tabs.Add(ClipsScreen.ScreenName);
            tabs.Add(MoreScreen.ScreenName);
            tabs.Add(PerformanceScreen.ScreenName);
            _expandButton = tabs.Add("Collapse >");
            InitToggleRightPanelButton(_expandButton);
            tabs.onTabSelected.AddListener(screenName =>
            {
                _screensManager.ChangeScreen(screenName);
                Expand(true);
            });

            _screensManager = InitScreensManager(rightPanel);
            _screensManager.onScreenChanged.AddListener(screenName => tabs.Select(screenName));
            _screensManager.Bind(plugin);
        }

        private static AnimationControlPanel CreateControlPanel(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 680f;

            return AnimationControlPanel.Configure(go);
        }

        private void InitChangeCurveTypeUI()
        {
            _curveType = CurveTypePopup.Create(_leftPanelPrefabFactory);
        }

        private Curves InitCurvesUI()
        {
            var go = new GameObject();
            go.transform.SetParent(leftPanel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 270f;
            layout.flexibleWidth = 1f;

            return go.AddComponent<Curves>();
        }

        protected void InitClipboardUI()
        {
            var container = _leftPanelPrefabFactory.CreateSpacer();
            container.height = 48f;

            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.childForceExpandWidth = true;
            group.childControlHeight = false;

            {
                var btn = Instantiate(_plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Cut";
                btn.button.onClick.AddListener(() => _plugin.cutJSON.actionCallback());
            }

            {
                var btn = Instantiate(_plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Copy";
                btn.button.onClick.AddListener(() => _plugin.copyJSON.actionCallback());
            }

            {
                var btn = Instantiate(_plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Paste";
                btn.button.onClick.AddListener(() => _plugin.pasteJSON.actionCallback());
            }
        }

        private void InitAutoKeyframeUI()
        {
            var autoKeyframeAllControllersUI = _leftPanelPrefabFactory.CreateToggle(_plugin.autoKeyframeAllControllersJSON);
        }

        private void InitToggleRightPanelButton(UIDynamicButton btn)
        {
            btn.button.onClick.RemoveAllListeners();
            btn.button.onClick.AddListener(() => Expand(!_expanded));
        }

        private void Expand(bool open)
        {
            if (!open && _expanded)
            {
                _expanded = false;
                _screensManager.enabled = false;
                rightPanel.GetComponent<LayoutElement>().preferredWidth = RightPanelCollapsedWidth;
                _expandButton.label = "< Expand";
            }
            else if (open && !_expanded)
            {
                _expanded = true;
                _screensManager.enabled = true;
                rightPanel.GetComponent<LayoutElement>().preferredWidth = RightPanelExpandedWidth;
                _expandButton.label = "Collapse >";
            }
        }

        private ScreensManager InitScreensManager(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();

            return ScreensManager.Configure(go);
        }

        public void Bind(AtomAnimation animation)
        {
            _controlPanel.Bind(animation);
            _curveType.Bind(animation);
            _curves.Bind(animation);
        }
    }
}
