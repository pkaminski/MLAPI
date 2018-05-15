﻿using MLAPI.Data.NetworkProfiler;
using MLAPI.NetworkingManagerComponents.Binary;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityEditor
{
    public class MLAPIProfiler : EditorWindow
    {
        [MenuItem("Window/MLAPI Profiler")]
        public static void ShowWindow()
        {
            GetWindow<MLAPIProfiler>();
        }

        GUIStyle wrapStyle
        {
            get
            {
                Color color = EditorStyles.label.normal.textColor;
                GUIStyle style = EditorStyles.centeredGreyMiniLabel;
                style.wordWrap = true;
                style.normal.textColor = color;
                return style;
            }
        }
        float updateDelay = 1f;
        int captureCount = 100;
        float showMax = 0;
        float showMin = 0;
        bool record = false;
        AnimationCurve curve = AnimationCurve.Constant(0, 1, 0);
        readonly List<ProfilerTick> currentTicks = new List<ProfilerTick>();
        float lastDrawn = 0;
        struct ProfilerContainer
        {
            public ProfilerTick[] ticks;
        }

        private void OnGUI()
        {
            if (!NetworkProfiler.IsRunning && record)
            {
                if (NetworkProfiler.Ticks != null && NetworkProfiler.Ticks.Count >= 2)
                    curve = AnimationCurve.Constant(NetworkProfiler.Ticks.ElementAt(0).Frame, NetworkProfiler.Ticks.ElementAt(NetworkProfiler.Ticks.Count - 1).Frame, 0);
                else
                    curve = AnimationCurve.Constant(0, 1, 0);

                lastDrawn = 0;
                NetworkProfiler.Start(captureCount);
            }

            //Draw top bar
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            bool prevRec = record;
            record = EditorGUILayout.Toggle("Record", record);

            if (GUILayout.Button("Import datafile"))
            {
                ProfilerTick[] ticks = BinarySerializer.Deserialize<ProfilerContainer>(File.ReadAllBytes(EditorUtility.OpenFilePanel("Choose a NetworkProfiler file", "", ""))).ticks;
                if (ticks.Length >= 2)
                    curve = AnimationCurve.Constant(ticks[0].EventId, ticks[(ticks.Length - 1)].EventId, 0);
                else
                    curve = AnimationCurve.Constant(0, 1, 0);
                currentTicks.Clear();
                for (int i = 0; i < ticks.Length; i++)
                {
                    currentTicks.Add(ticks[i]);

                    uint bytes = 0;
                    if (ticks[i].Events.Count > 0)
                    {
                        for (int j = 0; j < ticks[i].Events.Count; j++)
                        {
                            TickEvent tickEvent = ticks[i].Events[j];
                            bytes += tickEvent.Bytes;
                        }
                    }
                    curve.AddKey(ticks[i].EventId, bytes);
                }
            }

            if (GUILayout.Button("Export datafile"))
            {
                File.WriteAllBytes(EditorUtility.SaveFilePanel("Save NetworkProfiler data", "", "networkProfilerData", ""), BinarySerializer.Serialize(new ProfilerContainer() { ticks = currentTicks.ToArray() }));
            }

            EditorGUILayout.EndHorizontal();
            float prevHis = captureCount;
            captureCount = EditorGUILayout.DelayedIntField("History count", captureCount);
            if (captureCount <= 0)
                captureCount = 1;
            updateDelay = EditorGUILayout.Slider("Refresh delay", updateDelay, 0.1f, 10f);
            EditorGUILayout.EndVertical();

            if (prevRec != record)
            {
                if (prevRec)
                {
                    NetworkProfiler.Stop();
                }
                else
                {
                    if (NetworkProfiler.Ticks != null && NetworkProfiler.Ticks.Count >= 2)
                        curve = AnimationCurve.Constant(NetworkProfiler.Ticks.ElementAt(0).EventId, NetworkProfiler.Ticks.ElementAt(NetworkProfiler.Ticks.Count - 1).EventId, 0);
                    else
                        curve = AnimationCurve.Constant(0, 1, 0);
                    lastDrawn = 0;
                    NetworkProfiler.Start(captureCount);
                }
            }
            if (prevHis != captureCount)
            {
                NetworkProfiler.Stop();

                if (NetworkProfiler.Ticks != null && NetworkProfiler.Ticks.Count >= 2)
                    curve = AnimationCurve.Constant(NetworkProfiler.Ticks.ElementAt(0).EventId, NetworkProfiler.Ticks.ElementAt(NetworkProfiler.Ticks.Count - 1).EventId, 0);
                else
                    curve = AnimationCurve.Constant(0, 1, 0);

                lastDrawn = 0;
                NetworkProfiler.Start(captureCount);
            }

            //Cache
            if (NetworkProfiler.IsRunning)
            {
                if (Time.unscaledTime - lastDrawn > updateDelay)
                {
                    lastDrawn = Time.unscaledTime;
                    currentTicks.Clear();
                    if (NetworkProfiler.Ticks.Count >= 2)
                        curve = AnimationCurve.Constant(NetworkProfiler.Ticks.ElementAt(0).EventId, NetworkProfiler.Ticks.ElementAt(NetworkProfiler.Ticks.Count - 1).EventId, 0);

                    for (int i = 0; i < NetworkProfiler.Ticks.Count; i++)
                    {
                        ProfilerTick tick = NetworkProfiler.Ticks.ElementAt(i);
                        currentTicks.Add(tick);

                        uint bytes = 0;
                        if (tick.Events.Count > 0)
                        {
                            for (int j = 0; j < tick.Events.Count; j++)
                            {
                                TickEvent tickEvent = tick.Events[j];
                                bytes += tickEvent.Bytes;
                            }
                        }
                        curve.AddKey(tick.EventId, bytes);
                    }
                }
            }


            //Draw Animation curve and slider
            curve = EditorGUILayout.CurveField(curve);
            EditorGUILayout.MinMaxSlider(ref showMin, ref showMax, 0, currentTicks.Count);
            //Verify slider values
            if (showMin < 0)
                showMin = 0;
            if (showMax > currentTicks.Count)
                showMax = currentTicks.Count;
            if (showMin <= 0 && showMax <= 0)
            {
                showMin = 0;
                showMax = currentTicks.Count;
            }

            //Draw main board
            int nonEmptyTicks = 0;
            int largestTickCount = 0;
            int totalTicks = ((int)showMax - (int)showMin);

            for (int i = (int)showMin; i < (int)showMax; i++)
            {
                if (currentTicks[i].Events.Count > 0) nonEmptyTicks++; //Count non empty ticks
                if (currentTicks[i].Events.Count > largestTickCount) largestTickCount = currentTicks[i].Events.Count; //Get how many events the tick with most events has
            }
            int emptyTicks = totalTicks - nonEmptyTicks;

            float equalWidth = position.width / totalTicks;
            float propWidth = equalWidth * 0.3f;
            float widthPerTick = ((position.width - emptyTicks * propWidth) / nonEmptyTicks);

            float currentX = 0;
            int emptyStreak = 0;
            for (int i = (int)showMin; i < (int)showMax; i++)
            {
                ProfilerTick tick = currentTicks[i];
                if (tick.Events.Count == 0 && i != totalTicks - 1)
                {
                    emptyStreak++;
                    continue;
                }
                else if (emptyStreak > 0 || i == totalTicks - 1)
                {
                    Rect dataRect = new Rect(currentX, 100, propWidth * emptyStreak, position.height - 100);
                    currentX += propWidth * emptyStreak;
                    EditorGUI.LabelField(new Rect(dataRect.x, dataRect.y, dataRect.width, dataRect.height), emptyStreak.ToString(), wrapStyle);
                    emptyStreak = 0;
                }

                if (tick.Events.Count > 0)
                {
                    float heightPerEvent = ((position.height - 100f) - (5f * largestTickCount)) / largestTickCount;
                    float heightPerTotalBackground = ((position.height - 100f) - (5f * largestTickCount)) / tick.Events.Count;

                    float currentY = 100;
                    for (int j = 0; j < tick.Events.Count; j++)
                    {
                        TickEvent tickEvent = tick.Events[j];
                        Rect dataRect = new Rect(currentX, currentY, widthPerTick, heightPerEvent);
                        if (j == tick.Events.Count - 1)
                            dataRect.height -= 45f;
                        EditorGUI.DrawRect(dataRect, TickTypeToColor(tickEvent.EventType));
                        float heightPerField = heightPerEvent / 12f;
                        EditorGUI.LabelField(new Rect(dataRect.x, dataRect.y + heightPerField * -3f, dataRect.width, dataRect.height), "EventType: " + tickEvent.EventType.ToString(), wrapStyle);
                        EditorGUI.LabelField(new Rect(dataRect.x, dataRect.y + heightPerField * -1f, dataRect.width, dataRect.height), "Size: " + tickEvent.Bytes + "B", wrapStyle);
                        EditorGUI.LabelField(new Rect(dataRect.x, dataRect.y + heightPerField * 1f, dataRect.width, dataRect.height), "Channel: " + tickEvent.ChannelName, wrapStyle);
                        EditorGUI.LabelField(new Rect(dataRect.x, dataRect.y + heightPerField * 3f, dataRect.width, dataRect.height), "MessageType: " + tickEvent.MessageType, wrapStyle);

                        currentY += heightPerEvent + 5f;
                    }
                }
                EditorGUI.DrawRect(new Rect(currentX, position.height - 40, widthPerTick, 40), TickTypeToColor(tick.Type));
                EditorGUI.LabelField(new Rect(currentX, position.height - 40, widthPerTick, 20), "TickType: " + tick.Type.ToString(), wrapStyle);
                EditorGUI.LabelField(new Rect(currentX, position.height - 20, widthPerTick, 20), "Frame: " + tick.Frame.ToString(), wrapStyle);
                currentX += widthPerTick;
            }

            Repaint();
        }

        private static Color TickTypeToColor(TickType type)
        {
            switch (type)
            {
                case TickType.Event:
                    return new Color(0.58f, 0f, 0.56f, 0.37f);
                case TickType.Receive:
                    return new Color(0f, 0.85f, 0.85f, 0.28f);
                case TickType.Send:
                    return new Color(0, 0.55f, 1f, 0.06f);
            }
            return EditorGUIUtility.isProSkin ? new Color32(70, 70, 70, 255) : new Color32(200, 200, 200, 255);
        }
    }
}