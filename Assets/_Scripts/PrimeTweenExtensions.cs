using JetBrains.Annotations;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;

public static class PrimeTweenExtensions
{
    public static Sequence Jump([NotNull] Transform target, Vector3 endValue, float duration, float height,
        int numJumps = 1)
    {
        Assert.IsTrue(height > 0f);
        Assert.IsTrue(numJumps >= 1, nameof(numJumps) + "should be >= 1.");
        var jumpsSequence = Sequence.Create();
        var iniPosY = target.position.y;
        var deltaJump = (endValue.y - iniPosY) / numJumps;
        var jumpDuration = duration / (numJumps * 2);
        for (int i = 0; i < numJumps; i++)
        {
            var from = iniPosY + i * deltaJump;
            var to = iniPosY + (i + 1) * deltaJump;
            jumpsSequence.Chain(Tween.PositionY(target, Mathf.Max(from, to) + height, jumpDuration, Ease.OutQuad))
                .Chain(Tween.PositionY(target, to, jumpDuration, Ease.InQuad));
        }

        var result = Sequence.Create()
            .Group(jumpsSequence);
        if (!Mathf.Approximately(target.position.x, endValue.x))
        {
            result.Group(Tween.PositionX(target, endValue.x, duration, Ease.Linear));
        }

        if (!Mathf.Approximately(target.position.z, endValue.z))
        {
            result.Group(Tween.PositionZ(target, endValue.z, duration, Ease.Linear));
        }

        return result;
    }
}