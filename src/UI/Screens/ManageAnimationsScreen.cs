using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    public class ManageAnimationsScreen : ScreenBase
    {
        public const string ScreenName = "Manage Animations";

        public override string screenId => ScreenName;

        private JSONStorableString _animationsListJSON;
        private UIDynamicButton _deleteAnimationUI;
        private UIDynamicButton _deleteLayerUI;
        private UIDynamicButton _deleteSegmentUI;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", AnimationsScreen.ScreenName);

            InitAnimationsListUI();

            prefabFactory.CreateSpacer();

            InitReorderAnimationsUI();
            InitDeleteAnimationsUI();

            prefabFactory.CreateSpacer();

            InitReorderLayersUI();
            InitDeleteLayerUI();

            prefabFactory.CreateSpacer();

            InitReorderSegmentsUI();
            InitDeleteSegmentUI();

            prefabFactory.CreateSpacer();

            InitSyncInAllAtomsUI();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Create</b> anims/layers/segments...</i>", AddAnimationsScreen.ScreenName);

            RefreshAnimationsList();

            animation.onClipsListChanged.AddListener(RefreshAnimationsList);
        }

        private void InitAnimationsListUI()
        {
            _animationsListJSON = new JSONStorableString("Animations list", "");
            prefabFactory.CreateTextField(_animationsListJSON);
        }

        private void InitReorderAnimationsUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder animation (move up)");
            moveAnimUpUI.button.onClick.AddListener(ReorderAnimationMoveUp);

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder animation (move down)");
            moveAnimDownUI.button.onClick.AddListener(ReorderAnimationMoveDown);
        }

        private void InitDeleteAnimationsUI()
        {
            _deleteAnimationUI = prefabFactory.CreateButton("Delete animation");
            _deleteAnimationUI.button.onClick.AddListener(DeleteAnimation);
            _deleteAnimationUI.buttonColor = Color.red;
            _deleteAnimationUI.textColor = Color.white;
        }

        private void InitReorderLayersUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder layer (move up)");
            moveAnimUpUI.button.onClick.AddListener(ReorderLayerMoveUp);

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder layer (move down)");
            moveAnimDownUI.button.onClick.AddListener(ReorderLayerMoveDown);
        }

        private void InitDeleteLayerUI()
        {
            _deleteLayerUI = prefabFactory.CreateButton("Delete layer");
            _deleteLayerUI.button.onClick.AddListener(DeleteLayer);
            _deleteLayerUI.buttonColor = Color.red;
            _deleteLayerUI.textColor = Color.white;
        }

        private void InitReorderSegmentsUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder segment (move up)");
            moveAnimUpUI.button.onClick.AddListener(ReorderSegmentMoveUp);

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder segment (move down)");
            moveAnimDownUI.button.onClick.AddListener(ReorderSegmentMoveDown);
        }

        private void InitDeleteSegmentUI()
        {
            _deleteSegmentUI = prefabFactory.CreateButton("Delete segment");
            _deleteSegmentUI.button.onClick.AddListener(DeleteSegment);
            _deleteSegmentUI.buttonColor = Color.red;
            _deleteSegmentUI.textColor = Color.white;
        }

        private void InitSyncInAllAtomsUI()
        {
            var syncInAllAtoms = prefabFactory.CreateButton("Create/sync in all atoms");
            syncInAllAtoms.button.onClick.AddListener(SyncInAllAtoms);
        }

        #endregion

        #region Callbacks

        private void ReorderAnimationMoveUp()
        {
            var index = animation.clips.IndexOf(current);
            ReorderMove(
                index,
                index,
                index - 1,
                min: animation.clips.FindIndex(c => c.animationLayerQualified == current.animationLayerQualified)
            );
        }

        private void ReorderAnimationMoveDown()
        {
            var index = animation.clips.IndexOf(current);
            ReorderMove(
                index,
                index,
                index + 2,
                max: animation.clips.FindLastIndex(c => c.animationLayerQualified == current.animationLayerQualified) + 1
            );
        }

        private void DeleteAnimation()
        {
            prefabFactory.CreateConfirm("Delete current animation", DeleteAnimationConfirm);
        }

        private void DeleteAnimationConfirm()
        {
            var fallbackAnimation = currentLayer.TakeWhile(c => c != current).LastOrDefault() ?? currentLayer.FirstOrDefault(c => c != current);
            operations.AddAnimation().DeleteAnimation(current);
            animationEditContext.SelectAnimation(fallbackAnimation);
        }

        private void ReorderLayerMoveUp()
        {
            if (currentSegment.layerNames[0] == current.animationLayer) return;

            var previousLayer = currentSegment.layerNames[currentSegment.layerNames.IndexOf(current.animationLayer) - 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindLastIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindIndex(c => c.animationSegment == current.animationSegment && c.animationLayer == previousLayer)
            );
        }

        private void ReorderLayerMoveDown()
        {
            if (currentSegment.layerNames[currentSegment.layerNames.Count - 1] == current.animationLayer) return;

            var nextLayer = currentSegment.layerNames[currentSegment.layerNames.IndexOf(current.animationLayer) + 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindLastIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindLastIndex(c => c.animationSegment == current.animationSegment && c.animationLayer == nextLayer) + 1
            );
        }

        private void DeleteLayer()
        {
            prefabFactory.CreateConfirm("Delete current layer", DeleteLayerConfirm);
        }

        private void DeleteLayerConfirm()
        {
            if (currentSegment.layerNames.Count == 1)
            {
                SuperController.LogError("Timeline: Cannot delete the only layer.");
                return;
            }
            var clips = currentLayer;
            animationEditContext.SelectAnimation(animation.clips.First(c => c.animationSegment == current.animationSegment && c.animationLayer != current.animationLayer));
            foreach (var clip in clips)
                animation.RemoveClip(clip);
            animation.CleanupAnimatables();
        }

        private void ReorderSegmentMoveUp()
        {
            if (current.animationSegment == AtomAnimationClip.SharedAnimationSegment) return;
            if (animation.index.segmentNames[0] == current.animationSegment) return;

            var previousSegment = animation.index.segmentNames[animation.index.segmentNames.IndexOf(current.animationSegment) - 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindLastIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindIndex(c1 => c1.animationSegment == previousSegment)
            );
        }

        private void ReorderSegmentMoveDown()
        {
            if (current.animationSegment == AtomAnimationClip.SharedAnimationSegment) return;
            if (animation.index.segmentNames[animation.index.segmentNames.Count - 1] == current.animationSegment) return;

            var nextSegment = animation.index.segmentNames[animation.index.segmentNames.IndexOf(current.animationSegment) + 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindLastIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindLastIndex(c1 => c1.animationSegment == nextSegment) + 1
            );
        }

        private void DeleteSegment()
        {
            prefabFactory.CreateConfirm("Delete current segment", DeleteSegmentConfirm);
        }

        private void DeleteSegmentConfirm()
        {
            var segmentToDeleteId = current.animationSegmentId;
            var fallbackClip = animation.clips.First(c => c.animationSegmentId != segmentToDeleteId);
            if (animation.playingAnimationSegmentId == segmentToDeleteId)
                animation.playingAnimationSegment = fallbackClip.isOnSharedSegment ? null : fallbackClip.animationSegment;
            animationEditContext.SelectAnimation(fallbackClip);
            var clipsToDelete = animation.index.segmentsById[segmentToDeleteId].layers.SelectMany(c => c).ToList();
            foreach (var clip in clipsToDelete)
                animation.RemoveClip(clip);
            animation.CleanupAnimatables();
        }

        private void ReorderMove(int start, int end, int to, int min = 0, int max = int.MaxValue)
        {
            if (to < min || to > max)
            {
                return;
            }
            var count = end - start + 1;
            var clips = animation.clips.GetRange(start, count);
            animation.clips.RemoveRange(start, count);
            if (to > start) to -= count;
            animation.clips.InsertRange(to, clips);
            animation.index.Rebuild();
            // Realign quaternions
            var layers = clips.Select(c => c.animationLayerQualifiedId).Distinct();
            foreach (var clip in layers.Select(l => animation.index.ByLayerQualified(l)).SelectMany(l => l))
            {
                foreach (var t in clip.targetControllers)
                    t.dirty = true;
            }
            animation.onClipsListChanged.Invoke();
            animation.clipListChangedTrigger.Trigger();
        }

        private void SyncInAllAtoms()
        {
            plugin.peers.SendSyncAnimation(current);
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshAnimationsList();
        }

        private void RefreshAnimationsList()
        {
            var sb = new StringBuilder();

            var animationsInLayer = 0;
            foreach (var segment in animation.index.segmentsById)
            {
                if (segment.Key != current.animationSegmentId)
                {
                    sb.Append("<color=grey>");
                }

                if (animation.index.useSegment)
                {
                    if (segment.Key == current.animationSegmentId) sb.Append("<b>");
                    string segmentLabel;
                    if (segment.Key == AtomAnimationClip.SharedAnimationSegmentId)
                        segmentLabel = "[Shared]";
                    else if (segment.Key == AtomAnimationClip.NoneAnimationSegmentId)
                        segmentLabel = "Animations";
                    else
                        segmentLabel = segment.Value.mainClip.animationSegment;
                    sb.AppendLine($"{segmentLabel}");
                    if (segment.Key == current.animationSegmentId) sb.Append("</b>");
                }

                foreach (var layer in segment.Value.layers)
                {
                    if (layer[0].animationLayerQualified == current.animationLayerQualified) sb.Append("<b>");
                    sb.AppendLine($"- {layer[0].animationLayer}");
                    if (layer[0].animationLayerQualified == current.animationLayerQualified) sb.Append("</b>");

                    foreach (var clip in layer)
                    {
                        if (clip.animationLayerQualified == current.animationLayerQualified)
                            animationsInLayer++;

                        sb.Append("  - ");
                        if (clip == current) sb.Append("<b>");
                        sb.Append(clip.animationName);
                        if (clip == current) sb.Append("</b>");
                        sb.AppendLine();
                    }
                }

                if (segment.Key != current.animationSegmentId)
                {
                    sb.Append("</color>");
                }
            }

            _animationsListJSON.val = sb.ToString();
            _deleteAnimationUI.button.interactable = animationsInLayer > 1;
            _deleteLayerUI.button.interactable = currentSegment.layers.Count > 1;
            _deleteSegmentUI.button.interactable = animation.index.segmentNames.Count > 1;
        }

        public override void OnDestroy()
        {
            animation.onClipsListChanged.RemoveListener(RefreshAnimationsList);
            base.OnDestroy();
        }

        #endregion
    }
}

