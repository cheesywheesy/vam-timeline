﻿using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        SortedDictionary<int, KeyframeSettings> settings { get; }

        BezierAnimationCurve GetLeadCurve();
        IEnumerable<BezierAnimationCurve> GetCurves();
        void ChangeCurve(float time, string curveType, bool loop);
        void EnsureKeyframeSettings(float time, string defaultCurveTypeValue);
        string GetKeyframeSettings(float time);
    }
}
