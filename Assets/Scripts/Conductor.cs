﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Handles game music and keeps track of beats. </summary>
public class Conductor : MonoBehaviour
{
    [Tooltip("Beats per minute of music track")]
    public float SourceBPM;

    public int BarLength;

    public float TransitionBleed;
    
    AudioSource[] mMusicSources;
    
    // 0 if false, 1 if true
    bool mAudioSourcePlaying;

    double mSwapAudioSourceTime;
    bool mSourceSwapped;
    
    float mSourceBPS;

    double mOffset;

    private Queue<MusicFrame> MusicFrames;
    private MusicFrame mCurrentFrame;
    private MusicFrame mDeathFrame;


    public static Conductor GetActiveConductor()
    {
        return GetActiveConductorGameObject().GetComponent<Conductor>();
    }

    public static GameObject GetActiveConductorGameObject()
    {
        // If we are playing the game in order, then we will find the conductor in the scene
        GameObject conductor = GameObject.Find("Conductor");

        if (conductor == null)
        {
            conductor = (GameObject)GameObject.Instantiate(Resources.Load("Conductor"));
            
            conductor.name = "Conductor";
        }

        return conductor;
    }
    
    /// <summary> Get references to game objects, initialize settings, and start music. </summary>
    void Awake()
    {    
        // Get the audio sources attached to the game object
        mMusicSources = GetComponents<AudioSource>();
        
        mSourceBPS = SourceBPM / 60f;

        // Setup music frame config
        MusicFrames = new Queue<MusicFrame>();
        
        MusicFrames.Enqueue(new MusicFrame("Main Menu", 9.391F,9.391F,36.521F, false));
        MusicFrames.Enqueue(new MusicFrame("Level 1", 0.000F,82.434F,161.739F, true));
        MusicFrames.Enqueue(new MusicFrame("Level 2 Low Intensity", 136.695F,161.739F,270.260F,false));
        MusicFrames.Enqueue(new MusicFrame("Level 2 High Intensity", 270.260F,295.304F,351.652F,false));
        MusicFrames.Enqueue(new MusicFrame("Level 3 Low Intensity", 351.652F,355.826F,420.521F,false));
        MusicFrames.Enqueue(new MusicFrame("Level 3 High Intensity", 420.521F,441.391F,485.217F,true));
        MusicFrames.Enqueue(new MusicFrame("Credits", 485.217F,535.304F,566.000F,false));
        mDeathFrame = new MusicFrame("Death", 542.912F,542.912F,560.000F,true);
        
        mCurrentFrame = MusicFrames.Dequeue();

        mOffset  = AudioSettings.dspTime + 0.5;
        
        // Setup the first music source to play
        mMusicSources[0].PlayScheduled(mOffset);
        mMusicSources[0].time = mCurrentFrame.IntroStartTime;
        
        // Source 1 plays first
        mAudioSourcePlaying = true;
        
        // Setup AudioSource system swap helper variables
        mSwapAudioSourceTime = AudioSettings.dspTime + mCurrentFrame.LoopStartTime - mCurrentFrame.IntroStartTime;
        mSourceSwapped = true;
    }

    /// <summary> We track the time of the song ourselves instead of using AudioSettings.dspTime directly because the latter is not updated consistently and is not affected by time scale. </summary>
    void Update()
    {
        HandleLoop();
        
        if (AudioSettings.dspTime >= mSwapAudioSourceTime && !mSourceSwapped)
        {
            mAudioSourcePlaying = !mAudioSourcePlaying;
            mSourceSwapped = true;
        }
        
        if (Input.GetKeyDown(KeyCode.T))
        {
            RequestTransition();
        }
    }

    /// <summary> Returns time to/from nearest beat normalized from -0.5 to 0.5. </summary>
    /// <returns> Time to/from nearest beat normalized from -0.5 to 0.5 </returns>
    public float GetTimeToBeat()
    {
        double scaledBeatTime = (AudioSettings.dspTime - mOffset) * mSourceBPS;
        int nearestBeat = (int) (scaledBeatTime + 0.5f);
        return (float) (scaledBeatTime - nearestBeat);
    }

    /// <summary> Returns time since last beat normalized from 0 to 1. </summary>
    /// <returns> Time since last beat normalized from 0 to 1 </returns>
    public float GetTimeSinceBeat()
    {
        double scaledBeatTime = (AudioSettings.dspTime - mOffset) * mSourceBPS;
        return (float) (scaledBeatTime - (int) scaledBeatTime);
    }
    
    /// <summary> Returns index of the last played beat. </summary>
    /// <returns> Index of current beat </returns>
    public int GetBeat(int subdivision = 1)
    {
        double scaledBeatTime = (AudioSettings.dspTime - mOffset) * mSourceBPS * subdivision;
        return (int) scaledBeatTime;
    }

    public void RequestTransition()
    {
        HandleTransition(MusicFrames.Dequeue());
    }

    public void RequestDeathJingle()
    {
        HandleTransition(mDeathFrame);
    }

    public void SetVolume(float volume)
    {
        foreach (AudioSource a in mMusicSources)
        {
            a.volume = Mathf.Clamp01(volume);
        }
    }
    
    private void HandleLoop()
    {
        // Schedule a loop one bar after the last loop/transition occured
        if (AudioSettings.dspTime >= mSwapAudioSourceTime + (BarLength / mSourceBPS) && !(mMusicSources[0].isPlaying && mMusicSources[1].isPlaying))
        {
            AudioSource isPlaying = mMusicSources[0].isPlaying ? mMusicSources[0] : mMusicSources[1];
            AudioSource isStopped = mMusicSources[0].isPlaying ? mMusicSources[1] : mMusicSources[0];
            
            double loopAtTime = AudioSettings.dspTime + mCurrentFrame.LoopEndTime - isPlaying.time;
            
            isPlaying.SetScheduledEndTime(loopAtTime);
            isStopped.PlayScheduled(loopAtTime);
            isStopped.time = mCurrentFrame.LoopStartTime;
            
            mSwapAudioSourceTime = loopAtTime;
            mSourceSwapped = false;
            
            //Debug.Log($"Loop scheduled for : {loopAtTime}, time until loop : {loopAtTime - AudioSettings.dspTime}");
        }
    }

    private void HandleTransition(MusicFrame transitionTo)
    {
        AudioSource toPlay = mMusicSources[mAudioSourcePlaying ? 1 : 0];
        AudioSource toStop = mMusicSources[mAudioSourcePlaying ? 0 : 1];

        mCurrentFrame = transitionTo;
        toPlay.time = mCurrentFrame.IntroStartTime;

        // Find the timestamp for the next bar end
        int beat = GetBeat() % BarLength;
        double timeBetweenBeats = 1 / mSourceBPS;
        double timeToNextBeat = timeBetweenBeats - (GetTimeSinceBeat() * timeBetweenBeats);

        // Find the number of beats to wait (number of beats to wait until transitioning, we usually transition on the first beat of each bar)
        int beatsToWait = 0;
        if (!mCurrentFrame.TransitionImmediately)
        {
            beatsToWait = BarLength - (beat + 1);
        }

        double transitionTime = beatsToWait * timeBetweenBeats + timeToNextBeat + AudioSettings.dspTime;

        // If we transition immediately, we need to reset our offset, otherwise the bar count will get messed up
        if (mCurrentFrame.TransitionImmediately)
        {
            mOffset = transitionTime;
        }
        
        toPlay.PlayScheduled(transitionTime);
        toStop.SetScheduledEndTime(transitionTime);

        mSwapAudioSourceTime = transitionTime;
        mSourceSwapped = false;

        Debug.Log($"Next track queued: {mCurrentFrame.Name}, time until transition: {transitionTime - AudioSettings.dspTime}");
    }
}