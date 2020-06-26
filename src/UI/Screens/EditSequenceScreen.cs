using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class EditSequenceScreen : ScreenBase
    {
        public const string ScreenName = "Edit Sequence";

        public override string screenId => ScreenName;

        private JSONStorableBool _loop;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _nextAnimationJSON;
        private JSONStorableFloat _nextAnimationTimeJSON;
        private JSONStorableString _nextAnimationPreviewJSON;
        private JSONStorableBool _transitionJSON;
        private UIDynamicToggle _transitionUI;
        private UIDynamicToggle _loopUI;

        public EditSequenceScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            InitPreviewUI();

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            prefabFactory.CreateSpacer();

            InitSequenceUI();

            prefabFactory.CreateSpacer();

            InitTransitionUI();

            InitLoopUI();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Edit</b> animation settings...</i>", EditAnimationScreen.ScreenName, true);
            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);

            current.onAnimationSettingsModified.AddListener(OnAnimationSettingsModified);

            UpdateValues();
        }

        private void InitSequenceUI()
        {
            _nextAnimationJSON = new JSONStorableStringChooser("Next Animation", GetEligibleNextAnimations(), "", "Next Animation", (string val) => ChangeNextAnimation(val));
                        var nextAnimationUI = prefabFactory.CreateScrollablePopup(_nextAnimationJSON);
            nextAnimationUI.popupPanelHeight = 260f;

            _nextAnimationTimeJSON = new JSONStorableFloat("Next Blend After Seconds", 0f, (float val) => SetNextAnimationTime(val), 0f, 60f, false)
            {
                valNoCallback = current.nextAnimationTime
            };
                        var nextAnimationTimeUI = prefabFactory.CreateSlider(_nextAnimationTimeJSON);
            nextAnimationTimeUI.valueFormat = "F3";

            _blendDurationJSON = new JSONStorableFloat("BlendDuration", AtomAnimationClip.DefaultBlendDuration, v => UpdateBlendDuration(v), 0f, 5f, false);
                        var blendDurationUI = prefabFactory.CreateSlider(_blendDurationJSON);
            blendDurationUI.valueFormat = "F3";

            UpdateNextAnimationPreview();
        }

        private void InitPreviewUI()
        {
            _nextAnimationPreviewJSON = new JSONStorableString("Next Preview", "");
                        var nextAnimationResultUI = prefabFactory.CreateTextField(_nextAnimationPreviewJSON);
            nextAnimationResultUI.height = 30f;
        }

        private void InitTransitionUI()
        {
            var transitionLabelJSON = new JSONStorableString("Transition (Help)", "<b>Transition animations</b> can be enabled when there is an animation targeting the current animation, and when the current animation has a next animation configured. Only non-looping animations can be transition animations. This will automatically copy the last frame from the previous animation and the first frame from the next animation.");
                        var transitionLabelUI = prefabFactory.CreateTextField(transitionLabelJSON);
            // var layout = animationNameLabelUI.GetComponent<LayoutElement>();
            // layout.minHeight = 36f;
            transitionLabelUI.height = 340f;
            // UnityEngine.Object.Destroy(animationNameLabelUI.gameObject.GetComponentInChildren<Image>());

            _transitionJSON = new JSONStorableBool("Transition", false, (bool val) => ChangeTransition(val));
                        _transitionUI = prefabFactory.CreateToggle(_transitionJSON);
        }

        private void InitLoopUI()
        {
            _loop = new JSONStorableBool("Loop", current?.loop ?? true, (bool val) =>
            {
                current.loop = val;
                UpdateNextAnimationPreview();
                RefreshTransitionUI();
            });
                        _loopUI = prefabFactory.CreateToggle(_loop);
        }

        private void RefreshTransitionUI()
        {
            if (!current.transition)
            {
                if (current.loop)
                {
                    _transitionUI.toggle.interactable = false;
                    _loopUI.toggle.interactable = true;
                    return;
                }
                var clipsPointingToHere = animation.clips.Where(c => c != current && c.nextAnimationName == current.animationName).ToList();
                var targetClip = animation.clips.FirstOrDefault(c => c != current && c.animationName == current.nextAnimationName);
                if (clipsPointingToHere.Count == 0 || targetClip == null)
                {
                    _transitionUI.toggle.interactable = false;
                    _loopUI.toggle.interactable = true;
                    return;
                }

                if (clipsPointingToHere.Any(c => c.transition) || targetClip?.transition == true)
                {
                    _transitionUI.toggle.interactable = false;
                    _loopUI.toggle.interactable = true;
                    return;
                }
            }

            _transitionUI.toggle.interactable = true;
            _loopUI.toggle.interactable = !_transitionUI.toggle.isOn;
        }

        private void UpdateNextAnimationPreview()
        {
            if (current.nextAnimationName == null)
            {
                _nextAnimationPreviewJSON.val = "No next animation configured";
                return;
            }

            if (!current.loop)
            {
                _nextAnimationPreviewJSON.val = $"Will play once and blend at {current.nextAnimationTime}s";
                return;
            }

            if (_nextAnimationTimeJSON.val.IsSameFrame(0))
            {
                _nextAnimationPreviewJSON.val = "Will loop indefinitely";
            }
            else
            {
                _nextAnimationPreviewJSON.val = $"Will loop {Math.Round((current.nextAnimationTime + current.blendDuration) / current.animationLength, 2)} times including blending";
            }
        }

        private List<string> GetEligibleNextAnimations()
        {
            var animations = animation.clips
                .Where(c => c.animationLayer == current.animationLayer)
                .Select(c => c.animationName)
                .GroupBy(x =>
                {
                    var i = x.IndexOf("/");
                    if (i == -1) return null;
                    return x.Substring(0, i);
                });
            return new[] { "" }
                .Concat(animations.SelectMany(EnumerateAnimations))
                .Where(n => n != current.animationName)
                .Concat(new[] { AtomAnimation.RandomizeAnimationName })
                .ToList();
        }

        private IEnumerable<string> EnumerateAnimations(IGrouping<string, string> group)
        {
            foreach (var name in group)
                yield return name;

            if (group.Key != null)
                yield return group.Key + AtomAnimation.RandomizeGroupSuffix;
        }

        #endregion

        #region Callbacks

        private void UpdateBlendDuration(float v)
        {
            if (v < 0)
                _blendDurationJSON.valNoCallback = v = 0f;
            v = v.Snap();
            if (!current.loop && v >= (current.animationLength - 0.001f))
                _blendDurationJSON.valNoCallback = v = (current.animationLength - 0.001f).Snap();
            current.blendDuration = v;
        }

        private void ChangeTransition(bool val)
        {
            current.transition = val;
            RefreshTransitionUI();
            plugin.animation.Sample();
        }

        private void ChangeNextAnimation(string val)
        {
            current.nextAnimationName = val;
            SetNextAnimationTime(
                current.nextAnimationTime == 0
                ? current.nextAnimationTime = current.animationLength - current.blendDuration
                : current.nextAnimationTime
            );
            RefreshTransitionUI();
        }

        private void SetNextAnimationTime(float nextTime)
        {
            if (current.nextAnimationName == null)
            {
                _nextAnimationTimeJSON.valNoCallback = 0f;
                current.nextAnimationTime = 0f;
                return;
            }
            else if (!current.loop)
            {
                nextTime = (current.animationLength - current.blendDuration).Snap();
                current.nextAnimationTime = nextTime;
                _nextAnimationTimeJSON.valNoCallback = nextTime;
                return;
            }

            nextTime = nextTime.Snap();

            _nextAnimationTimeJSON.valNoCallback = nextTime;
            current.nextAnimationTime = nextTime;
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            args.before.onAnimationSettingsModified.RemoveListener(OnAnimationSettingsModified);
            args.after.onAnimationSettingsModified.AddListener(OnAnimationSettingsModified);

            UpdateValues();
        }

        private void OnAnimationSettingsModified(string _)
        {
            UpdateValues();
        }

        private void UpdateValues()
        {
            _blendDurationJSON.valNoCallback = current.blendDuration;
            _loop.valNoCallback = current.loop;
            _transitionJSON.valNoCallback = current.transition;
            _nextAnimationJSON.valNoCallback = current.nextAnimationName;
            _nextAnimationJSON.choices = GetEligibleNextAnimations();
            _nextAnimationTimeJSON.valNoCallback = current.nextAnimationTime;
            RefreshTransitionUI();
            UpdateNextAnimationPreview();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            current.onAnimationSettingsModified.RemoveListener(OnAnimationSettingsModified);
        }

        #endregion
    }
}
