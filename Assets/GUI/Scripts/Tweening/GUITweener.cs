using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Base class for all tweening operations.
/// </summary>
/// 
public class GUITweener : MonoBehaviour 
{
    /// <summary>
    /// Current tween that triggered the callback function.
    /// </summary>

    static public GUITweener current;

    public enum Method
    { 
        Linear,
        EaseIn,
        EaseInOut,
        BounceIn,
        BounceOut,
    }

    public enum Style
    { 
        Once,
        Loop,
        PingPong,
    }

    /// <summary>
    /// Tweening method used.
    /// </summary>

    [HideInInspector]
    public Method method = Method.Linear;

    /// <summary>
    /// Does it play once? Does it loop?
    /// </summary>

    [HideInInspector]
    public Style style = Style.Once;

    /// <summary>
    /// Optional curve to apply to the tween's time factor value.
    /// </summary>

    [HideInInspector]
    public AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 1f), new Keyframe(1f, 1f, 1f, 0f));

    /// <summary>
    /// Whether the tween will ignore the timescale, making it work while the game is paused.
    /// </summary>

    [HideInInspector]
    public bool ignoreTimeScale = true;

    /// <summary>
    /// How long will the tweener wait before starting the tween?
    /// </summary>

    [HideInInspector]
    public float delay = 0f;

    /// <summary>
    /// How long is the duration of the tween?
    /// </summary>
    
    [HideInInspector]
    public float duration = 1f;

    /// <summary>
    /// Whether the tweener will use steeper curves for ease in / out style interpolation.
    /// </summary>

    [HideInInspector]
    public bool steeperCurves = false;

    /// <summary>
    /// Used by buttons and tween sequences. Group of '0' means not in a sequence.
    /// </summary>

    [HideInInspector]
    public int tweenGroup = 0;

    /// <summary>
    /// Event delegates called when the animation finishes.
    /// </summary>

    [HideInInspector]
    public List<EventDelegate> onFinished = new List<EventDelegate>();

	// Deprecated functionality, kept for backwards compatilibity
    [HideInInspector]
    public GameObject eventReceiver;
    [HideInInspector]
    public string callWhenFinished;

    bool mStarted = false;
    float mStartTime = 0f;
    float mDuration = 0f;
    float mAmountPerDelta = 1000f;
    float mFactor = 0f;

    /// <summary>
    /// Amount advanced per delta time.
    /// </summary>

    public float amountPerDelta
    {
        get 
        {
            if (mDuration != duration)
            {
                mDuration = duration;
                mAmountPerDelta = Mathf.Abs((duration > 0f) ? 1f / duration : 1000f) * Mathf.Sign(mAmountPerDelta);
            }
            return mAmountPerDelta;
        }
    }

    /// <summary>
    /// Tween factor, 0-1 range.
    /// </summary>

    public float tweenFactor { get { return mFactor; } set { mFactor = Mathf.Clamp01(value); } }


    /// <summary>
    /// Direction that the tween is currently playing in.
    /// </summary>

    public GUIAnimationOrTween.Direction direction { get { return amountPerDelta < 0f ? GUIAnimationOrTween.Direction.Reverse : GUIAnimationOrTween.Direction.Forward; } }

    /// <summary>
    /// This function is called by Unity when you add a component. Automatically set the starting values for convenience.
    /// </summary>

    void Reset()
    {
        if (!mStarted)
        {
            SetStartToCurrentValue();
            SetEndToCurrentValue();
        }
    }

    /// <summary>
    /// Update as soon as it's started so that there is no delay.
    /// </summary>

    protected virtual void Start() { Update();  }

    /// <summary>
    /// Update the tweening factor and call the virtual update function.
    /// </summary>

    void Update()
    {
        float delta = ignoreTimeScale ? RealTime.deltaTime : Time.deltaTime;
        float time = ignoreTimeScale ? RealTime.time : Time.time;

        if (!mStarted)
        {
            mStarted = true;
            mStartTime = time + delay;
        }

        if (time < mStartTime)
            return;

        // Advance the sampling factor
        mFactor += amountPerDelta * delta;

        // Loop style simply resets the play factor after it exceeds 1
        if (style == Style.Loop)
        {
            if (mFactor > 1f)
            {
                mFactor -= Mathf.Floor(mFactor);
            }
        }
        else if (style == Style.PingPong)
        { 
            // Ping-Pong styloe reverses the direction
            if (mFactor > 1f)
            {
                mFactor = 1f - (mFactor - Mathf.Floor(mFactor));
                mAmountPerDelta -= mAmountPerDelta;
            }
            else if (mFactor < 0f)
            {
                mFactor = -mFactor;
                mFactor -= Mathf.Floor(mFactor);
                mAmountPerDelta = -mAmountPerDelta;
            }
        }

        // If the factor goes out of range and this is a one-time tweening operation, disable the script
        if ((style == Style.Once) && (duration == 0f || mFactor > 1f || mFactor < 0f))
        {
            mFactor = Mathf.Clamp01(mFactor);
            Sample(mFactor, true);

            // Disable this script unless the funciton calls above changed something
            if (duration == 0f || (mFactor == 1f && mAmountPerDelta > 0f || mFactor == 0f && mAmountPerDelta < 0f))
                enabled = false;

            if (current == null)
            {
                current = this;

                if (onFinished != null)
                {
                    mTemp = onFinished;
                    onFinished = new List<EventDelegate>();

                    // Notify the listener delegates
                    EventDelegate.Execute(mTemp);

                    // Re-add the previous persistent delegates
                    for (int i = 0; i < mTemp.Count; ++i)
                    {
                        EventDelegate ed = mTemp[i];
                        if (ed != null && !ed.oneShot)
                            EventDelegate.Add(onFinished, ed, ed.oneShot);
                    }

                    mTemp = null;
                }

                // Deprecated legacy functionality support
                if (eventReceiver != null && !string.IsNullOrEmpty(callWhenFinished))
                    eventReceiver.SendMessage(callWhenFinished, this, SendMessageOptions.DontRequireReceiver);

            }
        }
        else Sample(mFactor, false);
    }


    List<EventDelegate> mTemp = null;

    /// <summary>
    /// Convenience function -- set a new OnFinished event delegate (here for to be consistent with RemoveOnFinished).
    /// </summary>

    public void SetOnFinished(EventDelegate.Callback del)
    {
        EventDelegate.Set(onFinished, del);
    }

    /// <summary>
    /// Convenience function -- set a new OnFinished event delegate (here for to be consistent with RemoveOnFinished).
    /// </summary>

    public void SetOnFinished(EventDelegate del)
    {
        EventDelegate.Set(onFinished, del);
    }

    /// <summary>
    /// Convenience function -- add a new OnFinished event delegate (here for to be consistent with RemoveOnFinished).
    /// </summary>

    public void AddOnFinished(EventDelegate.Callback del) { EventDelegate.Add(onFinished, del); }

    /// <summary>
    /// Convenience function -- add a new OnFinished event delegate (here for to be consistent with RemoveOnFinished).
    /// </summary>

    public void AddOnFinished(EventDelegate del) { EventDelegate.Add(onFinished, del); }

    /// <summary>
    /// Remove an OnFinished delegate. Will work even while iterating through the list when the tweener has finished its operation.
    /// </summary>

    public void RemoveOnFinished(EventDelegate del)
    {
        if (onFinished != null) onFinished.Remove(del);
        if (mTemp != null) mTemp.Remove(del);
    }

    /// <summary>
    /// Mark as not started when finished to enable delay on next play.
    /// </summary>

    void OnDisable() { mStarted = false; }

    /// <summary>
    /// Sample the tween at the specified factor.
    /// </summary>


}
